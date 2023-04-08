using Camera;
using GPU.RT;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;
using ILGPUView2.GPU;
using ILGPUView2.GPU.DataStructures;
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

        public int ticks = 0;

        private volatile bool isRunning;
        private volatile bool isDrawing = false;
        
        private Thread renderThread;
        private Action<Device> onRender;
        
        private Stopwatch timer;
        private Queue<double> frameTimes = new Queue<double>();
        private double frameTimeSum = 0;

        public Device(RenderFrame renderFrame)
        {
            bool debug = false;
            this.renderFrame = renderFrame;

            context = Context.Create(builder => builder.CPU().Cuda().Assertions().EnableAlgorithms().Optimize(debug ? OptimizationLevel.Debug : OptimizationLevel.Debug));
            device = context.GetPreferredDevice(preferCPU: debug)
                                      .CreateAccelerator(context);
            kernels = new Dictionary<Type, object>();

            renderFrame.onResolutionChanged = (width, height) =>
            {
                if(framebuffer != null)
                {
                    framebuffer.Dispose();
                }

                framebuffer = new GPUImage(width, height);
            };
        }

        public void Start(Action<Device> onRender)
        {
            this.onRender = onRender;
            if (renderThread != null)
            {
                throw new InvalidOperationException("Render thread is already running.");
            }

            isRunning = true;
            renderThread = new Thread(Render)
            {
                IsBackground = true,
                Name = "Render Thread"
            };
            renderThread.Start();
        }

        public void Dispose()
        {
            Stop();

            framebuffer.Dispose();
            device.Dispose();
            context.Dispose();
        }

        public void Stop()
        {
            if (renderThread == null)
            {
                throw new InvalidOperationException("Render thread is not running.");
            }

            isRunning = false;
            renderThread.Join();
            renderThread = null;
        }

        public void Render()
        {
            timer = new Stopwatch();

            device.Synchronize();

            while (isRunning)
            {
                timer.Restart();

                if (framebuffer != null && !isDrawing)
                {
                    isDrawing = true;
                    
                    ticks++;
                    
                    onRender(this);
                    device.Synchronize();

                    framebuffer.toCPU();

                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            renderFrame.update(framebuffer.toCPU(), GetTimerString());
                        }
                        catch (Exception e)
                        {
                            Trace.WriteLine(e.ToString());
                        }

                        isDrawing = false;
                    });

                    while (isDrawing && isRunning)
                    {
                        // wait for isDrawing to be false or shutdown signal
                    }
                }

                //Thread.Sleep(15);

                UpdateTimer();
            }
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

        public void ExecuteFramebufferMask<TFunc>(GPUImage output, FrameBuffer input, TFunc filter = default) where TFunc : unmanaged, IFramebufferMask
        {
            var kernel = GetFramebufferMaskKernel(filter);
            kernel(output.width * output.height, ticks, output.toDevice(this), input, filter);
        }

        public void ExecuteVoxelFramebufferMask<TFunc>(Voxels voxels, FrameBuffer input, TFunc filter = default) where TFunc : unmanaged, IVoxelFramebufferFilter
        {
            var kernel = GetVoxelFramebufferFilterKernel(filter);
            kernel(new Index2D(voxels.xSize, voxels.ySize), ticks, voxels.toDevice(), input, filter);
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

        private Action<Index1D, int, dImage, FrameBuffer, TFunc> GetFramebufferMaskKernel<TFunc>(TFunc filter = default) where TFunc : unmanaged, IFramebufferMask
        {
            if (!kernels.ContainsKey(filter.GetType()))
            {
                Action<Index1D, int, dImage, FrameBuffer, TFunc> kernel = device.LoadAutoGroupedStreamKernel<Index1D, int, dImage, FrameBuffer, TFunc>(FramebufferMaskKernel);
                kernels.Add(filter.GetType(), kernel);
            }

            return (Action<Index1D, int, dImage, FrameBuffer, TFunc>)kernels[filter.GetType()];
        }

        private Action<Index2D, int, dVoxels, FrameBuffer, TFunc> GetVoxelFramebufferFilterKernel<TFunc>(TFunc filter = default) where TFunc : unmanaged, IVoxelFramebufferFilter
        {
            if (!kernels.ContainsKey(filter.GetType()))
            {
                Action<Index2D, int, dVoxels, FrameBuffer, TFunc> kernel = device.LoadAutoGroupedStreamKernel<Index2D, int, dVoxels, FrameBuffer, TFunc>(VoxelFramebufferFilterKernel);
                kernels.Add(filter.GetType(), kernel);
            }

            return (Action<Index2D, int, dVoxels, FrameBuffer, TFunc>)kernels[filter.GetType()];
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
