using GPU.RT;
using ILGPU.Runtime;
using ILGPU;
using ILGPUView2.GPU.DataStructures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static GPU.Kernels;

namespace GPU
{
    public struct Triangle
    {
        public Vec3 v0;
        public Vec3 v1;
        public Vec3 v2;

        public Triangle(Vec3 v0, Vec3 v1, Vec3 v2)
        {
            this.v0 = v0;
            this.v1 = v1;
            this.v2 = v2;
        }
    }

    public interface ITriangleImageFilter
    {
        RGBA32 Draw(int tick, float x, float y, dImage output, ArrayView1D<Triangle, Stride1D.Dense> triangles);
    }

    public static partial class Kernels
    {

        public static void TriangleImageFilterKernel<TFunc>(Index1D index, int tick, dImage output, ArrayView1D<Triangle, Stride1D.Dense> triangles, TFunc filter) where TFunc : unmanaged, ITriangleImageFilter
        {
            int x = index.X % output.width;
            int y = index.X / output.width;

            float u = (float)x / (float)output.width;
            float v = (float)y / (float)output.height;

            Vec2 uv = new Vec2(u, v);

            output.SetColorAt(x, y, filter.Draw(tick, uv.x, uv.y, output, triangles));
        }

    }

    public partial class Device
    {
        public void ExecuteTriangleFilter<TFunc>(GPUImage output, MemoryBuffer1D<Triangle, Stride1D.Dense> triangles, TFunc filter = default) where TFunc : unmanaged, ITriangleImageFilter
        {
            var kernel = GetTriangleImageFilterKernel(filter);
            kernel(output.width * output.height, ticks, output.toDevice(this), triangles, filter);
        }

        private Action<Index1D, int, dImage, ArrayView1D<Triangle, Stride1D.Dense>, TFunc> GetTriangleImageFilterKernel<TFunc>(TFunc filter = default) where TFunc : unmanaged, ITriangleImageFilter
        {
            if (!kernels.ContainsKey(filter.GetType()))
            {
                var kernel = device.LoadAutoGroupedStreamKernel<Index1D, int, dImage, ArrayView1D<Triangle, Stride1D.Dense>, TFunc>(TriangleImageFilterKernel);
                kernels.Add(filter.GetType(), kernel);
            }

            return (Action<Index1D, int, dImage, ArrayView1D<Triangle, Stride1D.Dense>, TFunc>)kernels[filter.GetType()];
        }
    }
}
