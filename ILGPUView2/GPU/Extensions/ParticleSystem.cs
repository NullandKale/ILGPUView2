using Camera;
using GPU;
using ILGPU;
using ILGPU.Runtime;
using ILGPUView2.GPU;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static GPU.Kernels;

namespace GPU
{
    public interface IParticleSystemFilter
    {
        RGBA32 Apply(int tick, float x, float y, dParticleSystem particles, dImage output);
    }

    public interface IParticleSystemDraw
    {
        void Draw(int tick, int particleID, dParticleSystem particles, dImage output);
    }

    public interface IParticleSystemUpdate
    {
        void Update(int tick, int id, dParticleSystem particles);
    }

    public static partial class Kernels
    {

        public static void ParticleSystemFilterKernel<TFunc>(Index1D index, int tick, dParticleSystem particles, dImage output, TFunc filter) where TFunc : unmanaged, IParticleSystemFilter
        {
            int x = index.X % output.width;
            int y = index.X / output.width;

            double u = (double)x / (double)output.width;
            double v = (double)y / (double)output.height;

            output.SetColorAt(x, y, filter.Apply(tick, (float)u, (float)v, particles, output));
        }

        public static void ParticleSystemDrawKernel<TFunc>(Index1D index, int tick, dParticleSystem particles, dImage output, TFunc filter) where TFunc : unmanaged, IParticleSystemDraw
        {
            filter.Draw(tick, index, particles, output);
        }

        public static void ParticleSystemUpdateKernel<TFunc>(Index1D index, int tick, dParticleSystem particles, TFunc filter) where TFunc : unmanaged, IParticleSystemUpdate
        {
            filter.Update(tick, index.X, particles);
        }
    }

    public partial class Renderer
    {
        public void DrawParticleSystem<TFunc>(GPUImage output, HostParticleSystem particleSystem, TFunc filter = default) where TFunc : unmanaged, IParticleSystemDraw
        {
            var kernel = GetParticleDrawKernel(filter);
            kernel(particleSystem.count, ticks, particleSystem.toGPU(), output.toDevice(this), filter);
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

        public void ExecuteParticleSystemFilter<TFunc>(GPUImage output, HostParticleSystem particleSystem, TFunc filter = default) where TFunc : unmanaged, IParticleSystemFilter
        {
            var kernel = GetParticleFilterKernel(filter);
            kernel(output.width * output.height, ticks, particleSystem.toGPU(), output.toDevice(this), filter);
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

        public void ExecuteParticleSystemUpdate<TFunc>(HostParticleSystem particleSystem, TFunc filter = default) where TFunc : unmanaged, IParticleSystemUpdate
        {
            var kernel = GetParticleUpdateKernel(filter);
            kernel(particleSystem.count, ticks, particleSystem.toGPU(), filter);
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
    }
}
