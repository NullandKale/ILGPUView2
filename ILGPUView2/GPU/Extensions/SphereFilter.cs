using Camera;
using GPU;
using GPU.RT;
using ILGPU;
using ILGPU.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static GPU.Kernels;

namespace GPU
{
    public interface ISphereImageFilter
    {
        RGBA32 Apply(int tick, float x, float y, dImage output, ArrayView1D<Sphere, Stride1D.Dense> spheres);
    }


    public static partial class Kernels
    {
        public static void SphereImageFilterKernel<TFunc>(Index1D index, int tick, dImage output, ArrayView1D<Sphere, Stride1D.Dense> spheres, TFunc filter) where TFunc : unmanaged, ISphereImageFilter
        {
            int x = index.X % output.width;
            int y = index.X / output.width;

            float u = (float)x / (float)output.width;
            float v = (float)y / (float)output.height;

            float min = 0.000001f;

            Vec2 uv = GetJitteredUV(tick, u, v, min, min);
            //Vec2 uv = new Vec2(u,v);

            output.SetColorAt(x, y, filter.Apply(tick, uv.x, uv.y, output, spheres));
        }

    }

    public partial class Renderer
    {
        public void ExecuteSphereFilter<TFunc>(GPUImage output, ArrayView1D<Sphere, Stride1D.Dense> spheres, TFunc filter = default) where TFunc : unmanaged, ISphereImageFilter
        {
            var kernel = GetSphereFilterKernel(filter);
            kernel(output.width * output.height, ticks, output.toDevice(this), spheres, filter);
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

    }
}
