using Camera;
using GPU.RT;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;
using ILGPUView2.GPU;
using ILGPUView2.GPU.DataStructures;
using ILGPUView2.GPU.Filters;
using ILGPUView2.GPU.RT;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using UIElement;
using static GPU.Kernels;

namespace GPU
{
    public partial class Renderer : IDisposable
    {
        public GPUImage framebuffer;
        public RenderFrame renderFrame;
        public Context context;
        public Accelerator device;

        protected Dictionary<Type, object> kernels;
        protected Action<Index1D, ArrayView1D<byte, Stride1D.Dense>, dImage> rgbKernel;
        protected Action<Index1D, dImage, ArrayView1D<byte, Stride1D.Dense>> rgbaKernel;
        protected Action<Index1D, dImage, FilterDepth> filterDepth;

        public int ticks = 0;

        protected volatile bool isRunning;
        protected volatile bool isDrawing = false;
        protected Thread renderThread;
        protected Action<Renderer> onRender;
        protected Action<Renderer> onLateRender;

        protected Stopwatch timer;
        protected Queue<double> frameTimes = new Queue<double>();
        protected double frameTimeSum = 0;

        public Renderer(RenderFrame renderFrame)
        {
            bool debug = false;
            this.renderFrame = renderFrame;

            context = Context.Create(builder => builder.CPU().Cuda().
                                                        EnableAlgorithms().
                                                        Math(MathMode.Fast32BitOnly).
                                                        Inlining(InliningMode.Aggressive).
                                                        AutoAssertions().
                                                        Optimize(OptimizationLevel.O1));
            device = context.GetPreferredDevice(preferCPU: debug).CreateAccelerator(context);
            kernels = new Dictionary<Type, object>();

            rgbKernel = device.LoadAutoGroupedStreamKernel<Index1D, ArrayView1D<byte, Stride1D.Dense>, dImage>(ImageToRGB);
            rgbaKernel = device.LoadAutoGroupedStreamKernel<Index1D, dImage, ArrayView1D<byte, Stride1D.Dense>>(RGBToImage);
            filterDepth = device.LoadAutoGroupedStreamKernel<Index1D, dImage, FilterDepth>(FilteredDepthKernel);

            renderFrame.onResolutionChanged = (width, height) =>
            {
                if(framebuffer != null)
                {
                    framebuffer.Dispose();
                }

                framebuffer = new GPUImage(width, height);
            };
        }

        public void Start(Action<Renderer> onRender, Action<Renderer> onLateRender)
        {
            this.onRender = onRender;
            this.onLateRender = onLateRender;

            if (renderThread != null)
            {
                throw new InvalidOperationException("Render thread is already running.");
            }

            isRunning = true;
            renderThread = new Thread(Render)
            {
                IsBackground = true,
                Name = "Render Thread",
                Priority = ThreadPriority.Highest
            };
            renderThread.Start();
        }

        public void Dispose()
        {
            isRunning = false;
        }

        public void Render()
        {
            timer = new Stopwatch();

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);

            while (isRunning)
            {
                timer.Restart();

                if (framebuffer != null && !isDrawing)
                {
                    isDrawing = true;
                    
                    ticks++;
                    
                    onRender(this);
                    device.Synchronize();

                    var frameData = framebuffer.toCPU();

                    try
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (isRunning)
                            {
                                Application.Current.MainWindow.Title = GetTimerString();
                                renderFrame.update(ref frameData);
                                isDrawing = false;
                            }
                        }, System.Windows.Threading.DispatcherPriority.Render);
                    }
                    catch(Exception e)
                    {
                        Trace.WriteLine(e);
                    }

                    onLateRender(this);
                }

                UpdateTimer();
            }

            framebuffer.Dispose();
            device.Dispose();
            context.Dispose();
        }

        private void UpdateTimer()
        {
            timer.Stop();

            if (frameTimes.Count > 10)
            {
                double f = frameTimes.Dequeue();
                frameTimeSum -= f;
            }

            frameTimes.Enqueue(timer.Elapsed.TotalMilliseconds);
            frameTimeSum += timer.Elapsed.TotalMilliseconds;
        }

        private string GetTimerString()
        {
            double frameTimeMS = frameTimeSum / frameTimes.Count;
            double FPS = 1000.0 / frameTimeMS;

            return $"FPS: {FPS:0.00} {(frameTimeMS):0.00}MS";
        }

        public void Execute<T, TFunc>(GPUBuffer<T> GPUBuffer, TFunc func = default) where T : unmanaged where TFunc : unmanaged, IKernel<T>
        {
            var kernel = GetKernel<T, TFunc>(func);
            kernel((int)GPUBuffer.size, ticks, GPUBuffer.toGPU(), func);
        }

        private Action<Index1D, int, dBuffer<T>, TFunc> GetKernel<T, TFunc>(TFunc filter = default) where TFunc : unmanaged, IKernel<T> where T : unmanaged
        {
            if (!kernels.ContainsKey(filter.GetType()))
            {
                var kernel = device.LoadAutoGroupedStreamKernel<Index1D, int, dBuffer<T>, TFunc>(Kernels.KernelKernel);
                kernels.Add(filter.GetType(), kernel);
            }

            return (Action<Index1D, int, dBuffer<T>, TFunc>)kernels[filter.GetType()];
        }

        public void ExecuteMask<T, TFunc>(GPUImage output, GPUBuffer<T> GPUBuffer, TFunc func = default) where T : unmanaged where TFunc : unmanaged, IKernelMask<T>
        {
            var kernel = GetKernelMask<T, TFunc>(func);
            kernel((int)GPUBuffer.size, ticks, output.toDevice(this), GPUBuffer.toGPU(), func);
        }

        private Action<Index1D, int, dImage, dBuffer<T>, TFunc> GetKernelMask<T, TFunc>(TFunc filter = default) where TFunc : unmanaged, IKernelMask<T> where T : unmanaged
        {
            if (!kernels.ContainsKey(filter.GetType()))
            {
                var kernel = device.LoadAutoGroupedStreamKernel<Index1D, int, dImage, dBuffer<T>, TFunc>(Kernels.KernelMaskKernel);
                kernels.Add(filter.GetType(), kernel);
            }

            return (Action<Index1D, int, dImage, dBuffer<T>, TFunc>)kernels[filter.GetType()];
        }

        public void ExecuteDepthFilter(GPUImage output, FilterDepth filter)
        {
            filterDepth(output.width * output.height, output.toDevice(this), filter);
        }

        public void CopyToRGB(MemoryBuffer1D<byte, Stride1D.Dense> output, GPUImage input)
        {
            rgbKernel(input.width * input.height, output, input.toDevice(this));
            device.Synchronize();
        }

        public void RGBtoRGBA(GPUImage output, MemoryBuffer1D<byte, Stride1D.Dense> input)
        {
            rgbaKernel(output.width * output.height, output.toDevice(this), input);
            device.Synchronize();
        }


    }
}
