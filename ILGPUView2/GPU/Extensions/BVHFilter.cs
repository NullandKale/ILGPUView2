using GPU;
using ILGPU;
using ILGPU.Runtime;
using ILGPUView2.GPU.RT;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static GPU.Kernels;

namespace GPU
{
    public interface IBVHImageFilter
    {
        RGBA32 Apply(int tick, float x, float y, dImage output, DEVICE_BVH bvh);
    }

    public static partial class Kernels
    {

        public static void BVHImageFilterKernel<TFunc>(Index1D index, int tick, dImage output, DEVICE_BVH bvh, TFunc filter) where TFunc : unmanaged, IBVHImageFilter
        {
            int x = index.X % output.width;
            int y = index.X / output.width;

            float u = (float)x / (float)output.width;
            float v = (float)y / (float)output.height;

            float min = 0.000001f;

            Vec2 uv = GetJitteredUV(tick, u, v, min, min);
            //Vec2 uv = new Vec2(u,v);

            output.SetColorAt(x, y, filter.Apply(tick, uv.x, uv.y, output, bvh));
        }

    }

    public partial class Renderer
    {
        public void ExecuteBVHFilter<TFunc>(GPUImage output, DEVICE_BVH bvh, TFunc filter = default) where TFunc : unmanaged, IBVHImageFilter
        {
            var kernel = GetBVHFilterKernel(filter);
            kernel(output.width * output.height, ticks, output.toDevice(this), bvh, filter);
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
    }
}
