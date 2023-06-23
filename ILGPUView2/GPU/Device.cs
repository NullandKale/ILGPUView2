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
    public class Device : IDisposable
    {
        public GPUImage framebuffer;
        public RenderFrame renderFrame;
        public Context context;
        public Accelerator device;

        private Dictionary<Type, object> kernels;
        private Action<Index1D, ArrayView1D<byte, Stride1D.Dense>, dImage> rgbKernel;
        private Action<Index1D, dImage, ArrayView1D<byte, Stride1D.Dense>> rgbaKernel;
        private Action<Index1D, dImage, FilterDepth> filterDepth;

        public int ticks = 0;

        private volatile bool isRunning;
        private volatile bool isDrawing = false;
        
        private Thread renderThread;
        private Action<Device> onRender;
        private Action<Device> onLateRender;

        private Stopwatch timer;
        private Queue<double> frameTimes = new Queue<double>();
        private double frameTimeSum = 0;
        public Device(RenderFrame renderFrame)
        {
            bool debug = false;
            this.renderFrame = renderFrame;

            context = Context.Create(builder => builder.CPU().Cuda().
                                                        EnableAlgorithms().
                                                        Math(MathMode.Fast).
                                                        Inlining(InliningMode.Aggressive).
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

        public void Start(Action<Device> onRender, Action<Device> onLateRender)
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

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if(isRunning)
                        {
                            Application.Current.MainWindow.Title = GetTimerString();
                            renderFrame.update(ref frameData);
                            isDrawing = false;
                        }
                    }, System.Windows.Threading.DispatcherPriority.Render);

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

        public void ExecuteMask<T, TFunc>(GPUImage output, GPUBuffer<T> GPUBuffer, TFunc func = default) where T : unmanaged where TFunc : unmanaged, IKernelMask<T>
        {
            var kernel = GetKernelMask<T, TFunc>(func);
            kernel((int)GPUBuffer.size, ticks, output.toDevice(this), GPUBuffer.toGPU(), func);
        }

        public void ExecuteFilter<TFunc>(GPUImage output, TFunc filter = default) where TFunc : unmanaged, IImageFilter
        {
            var kernel = GetFilterKernel(filter);
            kernel(output.width * output.height, ticks, output.toDevice(this), filter);
        }

        public void ExecuteParticleSystemFilter<TFunc>(GPUImage output, HostParticleSystem particleSystem, TFunc filter = default) where TFunc : unmanaged, IParticleSystemFilter
        {
            var kernel = GetParticleFilterKernel(filter);
            kernel(output.width * output.height, ticks, particleSystem.toGPU(), output.toDevice(this), filter);
        }

        public void DrawParticleSystem<TFunc>(GPUImage output, HostParticleSystem particleSystem, TFunc filter = default) where TFunc : unmanaged, IParticleSystemDraw
        {
            var kernel = GetParticleDrawKernel(filter);
            kernel(particleSystem.count, ticks, particleSystem.toGPU(), output.toDevice(this), filter);
        }

        public void ExecuteParticleSystemUpdate<TFunc>(HostParticleSystem particleSystem, TFunc filter = default) where TFunc : unmanaged, IParticleSystemUpdate
        {
            var kernel = GetParticleUpdateKernel(filter);
            kernel(particleSystem.count, ticks, particleSystem.toGPU(), filter);
        }

        public void ExecuteSphereFilter<TFunc>(GPUImage output, ArrayView1D<Sphere, Stride1D.Dense> spheres, TFunc filter = default) where TFunc : unmanaged, ISphereImageFilter
        {
            var kernel = GetSphereFilterKernel(filter);
            kernel(output.width * output.height, ticks, output.toDevice(this), spheres, filter);
        }

        public void ExecuteBVHFilter<TFunc>(GPUImage output, DEVICE_BVH bvh, TFunc filter = default) where TFunc : unmanaged, IBVHImageFilter
        {
            var kernel = GetBVHFilterKernel(filter);
            kernel(output.width * output.height, ticks, output.toDevice(this), bvh, filter);
        }

        public void ExecuteMask<TFunc>(GPUImage output, GPUImage input, TFunc filter = default) where TFunc : unmanaged, IImageMask
        {
            var kernel = GetMaskKernel(filter);
            kernel(output.width * output.height, ticks, output.toDevice(this), input.toDevice(this), filter);
        }

        public void ExecuteIntMask<TFunc>(GPUImage output, GPUImage input, TFunc filter = default) where TFunc : unmanaged, IIntImageMask
        {
            var kernel = GetIntMaskKernel(filter);
            kernel(output.width * output.height, ticks, output.toDevice(this), input.toDevice(this), filter);
        }

        public void ExecuteDepthFilter(GPUImage output, FilterDepth filter)
        {
            filterDepth(output.width * output.height, output.toDevice(this), filter);
        }

        public void ExecuteTexturedMask<TFunc>(GPUImage output, GPUImage mask, GPUImage texture, TFunc filter = default) where TFunc : unmanaged, ITexturedMask
        {
            var kernel = GetTexturedMaskKernel(filter);
            kernel(output.width * output.height, ticks, output.toDevice(this), mask.toDevice(this), texture.toDevice(this), filter);
        }

        public void Execute3TextureMask<TFunc>(GPUImage output, GPUImage mask, GPUImage texture0, GPUImage texture1, TFunc filter = default) where TFunc : unmanaged, I3TextureMask
        {
            var kernel = Get3TextureMaskKernel(filter);
            kernel(output.width * output.height, ticks, output.toDevice(this), mask.toDevice(this), texture0.toDevice(this), texture1.toDevice(this), filter);
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

        public void ExecuteFramebufferMask<TFunc>(GPUImage output, FrameBuffer input, TFunc filter = default) where TFunc : unmanaged, IFramebufferMask
        {
            var kernel = GetFramebufferMaskKernel(filter);
            kernel(output.width * output.height, ticks, output.toDevice(this), input, filter);
        }

        public void ExecuteVoxelFramebufferMask<TFunc>(Voxels voxels, GPUImage depth, GPUImage color, TFunc filter = default) where TFunc : unmanaged, IVoxelMask
        {
            var kernel = GetVoxelFramebufferFilterKernel(filter);
            kernel(new Index2D(voxels.xSize, voxels.ySize), ticks, voxels.toDevice(), depth.toDevice(this), color.toDevice(this), filter);
        }

        public void ExecuteVoxelFilter<TFunc>(GPUImage output, Voxels voxels, TFunc filter = default) where TFunc : unmanaged, IVoxelFilter
        {
            var kernel = GetVoxelFilterKernel(filter);
            kernel(output.width * output.height, ticks, voxels.toDevice(), output.toDevice(this), filter);
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

        private Action<Index1D, int, dImage, dBuffer<T>, TFunc> GetKernelMask<T, TFunc>(TFunc filter = default) where TFunc : unmanaged, IKernelMask<T> where T : unmanaged
        {
            if (!kernels.ContainsKey(filter.GetType()))
            {
                var kernel = device.LoadAutoGroupedStreamKernel<Index1D, int, dImage, dBuffer<T>, TFunc>(Kernels.KernelMaskKernel);
                kernels.Add(filter.GetType(), kernel);
            }

            return (Action<Index1D, int, dImage, dBuffer<T>, TFunc>)kernels[filter.GetType()];
        }

        private Action<Index1D, int, dImage, TFunc> GetFilterKernel<TFunc>(TFunc filter = default) where TFunc : unmanaged, IImageFilter
        {
            if (!kernels.ContainsKey(filter.GetType()))
            {
                var kernel = device.LoadAutoGroupedStreamKernel<Index1D, int, dImage, TFunc>(Kernels.ImageFilterKernel);
                kernels.Add(filter.GetType(), kernel);
            }

            return (Action<Index1D, int, dImage, TFunc>)kernels[filter.GetType()];
        }

        private Action<Index1D, int, dParticleSystem, dImage, TFunc> GetParticleDrawKernel<TFunc>(TFunc filter = default) where TFunc : unmanaged, IParticleSystemDraw
        {
            if (!kernels.ContainsKey(filter.GetType()))
            {
                var kernel = device.LoadAutoGroupedStreamKernel<Index1D, int, dParticleSystem, dImage, TFunc>(Kernels.ParticleSystemDrawKernel);
                kernels.Add(filter.GetType(), kernel);
            }

            return (Action<Index1D, int, dParticleSystem, dImage, TFunc>)kernels[filter.GetType()];
        }

        private Action<Index1D, int, dParticleSystem, dImage, TFunc> GetParticleFilterKernel<TFunc>(TFunc filter = default) where TFunc : unmanaged, IParticleSystemFilter
        {
            if (!kernels.ContainsKey(filter.GetType()))
            {
                var kernel = device.LoadAutoGroupedStreamKernel<Index1D, int, dParticleSystem, dImage, TFunc>(Kernels.ParticleSystemFilterKernel);
                kernels.Add(filter.GetType(), kernel);
            }

            return (Action<Index1D, int, dParticleSystem, dImage, TFunc>)kernels[filter.GetType()];
        }

        private Action<Index1D, int, dParticleSystem, TFunc> GetParticleUpdateKernel<TFunc>(TFunc filter = default) where TFunc : unmanaged, IParticleSystemUpdate
        {
            if (!kernels.ContainsKey(filter.GetType()))
            {
                var kernel = device.LoadAutoGroupedStreamKernel<Index1D, int, dParticleSystem, TFunc>(Kernels.ParticleSystemUpdateKernel);
                kernels.Add(filter.GetType(), kernel);
            }

            return (Action<Index1D, int, dParticleSystem, TFunc>)kernels[filter.GetType()];
        }

        private Action<Index1D, int, dImage, ArrayView1D<Sphere, Stride1D.Dense>, TFunc> GetSphereFilterKernel<TFunc>(TFunc filter = default) where TFunc : unmanaged, ISphereImageFilter
        {
            if (!kernels.ContainsKey(filter.GetType()))
            {
                var kernel = device.LoadAutoGroupedStreamKernel<Index1D, int, dImage, ArrayView1D<Sphere, Stride1D.Dense>, TFunc>(SphereImageFilterKernel);
                kernels.Add(filter.GetType(), kernel);
            }

            return (Action<Index1D, int, dImage, ArrayView1D<Sphere, Stride1D.Dense>, TFunc>)kernels[filter.GetType()];
        }

        private Action<Index1D, int, dImage, DEVICE_BVH, TFunc> GetBVHFilterKernel<TFunc>(TFunc filter = default) where TFunc : unmanaged, IBVHImageFilter
        {
            if (!kernels.ContainsKey(filter.GetType()))
            {
                var kernel = device.LoadAutoGroupedStreamKernel<Index1D, int, dImage, DEVICE_BVH, TFunc>(BVHImageFilterKernel);
                kernels.Add(filter.GetType(), kernel);
            }

            return (Action<Index1D, int, dImage, DEVICE_BVH, TFunc>)kernels[filter.GetType()];
        }

        private Action<Index1D, int, dImage, dImage, TFunc> GetMaskKernel<TFunc>(TFunc filter = default) where TFunc : unmanaged, IImageMask
        {
            if (!kernels.ContainsKey(filter.GetType()))
            {
                Action<Index1D, int, dImage, dImage, TFunc> kernel = device.LoadAutoGroupedStreamKernel<Index1D, int, dImage, dImage, TFunc>(ImageMaskKernel);
                kernels.Add(filter.GetType(), kernel);
            }

            return (Action<Index1D, int, dImage, dImage, TFunc>)kernels[filter.GetType()];
        }

        private Action<Index1D, int, dImage, dImage, TFunc> GetIntMaskKernel<TFunc>(TFunc filter = default) where TFunc : unmanaged, IIntImageMask
        {
            if (!kernels.ContainsKey(filter.GetType()))
            {
                Action<Index1D, int, dImage, dImage, TFunc> kernel = device.LoadAutoGroupedStreamKernel<Index1D, int, dImage, dImage, TFunc>(IntImageMaskKernel);
                kernels.Add(filter.GetType(), kernel);
            }

            return (Action<Index1D, int, dImage, dImage, TFunc>)kernels[filter.GetType()];
        }

        private Action<Index1D, int, dImage, dImage, dImage, TFunc> GetTexturedMaskKernel<TFunc>(TFunc filter = default) where TFunc : unmanaged, ITexturedMask
        {
            if (!kernels.ContainsKey(filter.GetType()))
            {
                Action<Index1D, int, dImage, dImage, dImage, TFunc> kernel = device.LoadAutoGroupedStreamKernel<Index1D, int, dImage, dImage, dImage, TFunc>(TexturedMaskKernel);
                kernels.Add(filter.GetType(), kernel);
            }

            return (Action<Index1D, int, dImage, dImage, dImage, TFunc>)kernels[filter.GetType()];
        }

        private Action<Index1D, int, dImage, dImage, dImage, dImage, TFunc> Get3TextureMaskKernel<TFunc>(TFunc filter = default) where TFunc : unmanaged, I3TextureMask
        {
            if (!kernels.ContainsKey(filter.GetType()))
            {
                Action<Index1D, int, dImage, dImage, dImage, dImage, TFunc > kernel = device.LoadAutoGroupedStreamKernel<Index1D, int, dImage, dImage, dImage, dImage, TFunc>(ThreeTextureMaskKernel);
                kernels.Add(filter.GetType(), kernel);
            }

            return (Action<Index1D, int, dImage, dImage, dImage, dImage, TFunc >)kernels[filter.GetType()];
        }

        private Action<Index1D, int, dImage, FrameBuffer, TFunc> GetFramebufferMaskKernel<TFunc>(TFunc filter = default) where TFunc : unmanaged, IFramebufferMask
        {
            if (!kernels.ContainsKey(filter.GetType()))
            {
                Action<Index1D, int, dImage, FrameBuffer, TFunc> kernel = device.LoadAutoGroupedStreamKernel<Index1D, int, dImage, FrameBuffer, TFunc>(FramebufferMaskKernel);
                kernels.Add(filter.GetType(), kernel);
            }

            return (Action<Index1D, int, dImage, FrameBuffer, TFunc>)kernels[filter.GetType()];
        }

        private Action<Index2D, int, dVoxels, dImage, dImage, TFunc> GetVoxelFramebufferFilterKernel<TFunc>(TFunc filter = default) where TFunc : unmanaged, IVoxelMask
        {
            if (!kernels.ContainsKey(filter.GetType()))
            {
                Action<Index2D, int, dVoxels, dImage, dImage, TFunc> kernel = device.LoadAutoGroupedStreamKernel<Index2D, int, dVoxels, dImage, dImage, TFunc>(VoxelFramebufferFilterKernel);
                kernels.Add(filter.GetType(), kernel);
            }

            return (Action<Index2D, int, dVoxels, dImage, dImage, TFunc>)kernels[filter.GetType()];
        }

        private Action<Index1D, int, dVoxels, dImage, TFunc> GetVoxelFilterKernel<TFunc>(TFunc filter = default) where TFunc : unmanaged, IVoxelFilter
        {
            if (!kernels.ContainsKey(filter.GetType()))
            {
                Action<Index1D, int, dVoxels, dImage, TFunc> kernel = device.LoadAutoGroupedStreamKernel<Index1D, int, dVoxels, dImage, TFunc>(VoxelFilterKernel);
                kernels.Add(filter.GetType(), kernel);
            }

            return (Action<Index1D, int, dVoxels, dImage, TFunc>)kernels[filter.GetType()];
        }
    }
}
