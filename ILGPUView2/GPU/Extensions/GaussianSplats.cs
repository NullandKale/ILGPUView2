using GPU;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using ILGPUView2.GPU.DataStructures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static GPU.Kernels;

namespace GPU
{
    public interface ISplatFramebufferMask
    {
        RGBA32 Apply(int tick, float x, float y, FrameBuffer output, ArrayView1D<SplatData, Stride1D.Dense> splats);
    }

    public static partial class Kernels
    {
        public static void SplatFramebufferMaskKernel<TFunc>(Index1D index, int tick, FrameBuffer output, ArrayView1D<SplatData, Stride1D.Dense> splats, TFunc filter) where TFunc : unmanaged, ISplatFramebufferMask
        {
            int x = index.X % output.width;
            int y = index.X / output.width;

            double u = (double)x / (double)output.width;
            double v = (double)y / (double)output.height;

            output.SetColorAt(x, y, filter.Apply(tick, (float)u, (float)v, output, splats));
        }
    }

    public partial class Renderer
    {
        public void ExecuteSplatFramebufferMaskKernel<TFunc>(FrameBuffer output, ArrayView1D<SplatData, Stride1D.Dense> splats, TFunc filter = default) where TFunc : unmanaged, ISplatFramebufferMask
        {
            var kernel = GetExecuteSplatFramebufferMaskKernel(filter);
            kernel(output.width * output.height, ticks, output, splats, filter);
        }

        private Action<Index1D, int, FrameBuffer, ArrayView1D<SplatData, Stride1D.Dense>, TFunc> GetExecuteSplatFramebufferMaskKernel<TFunc>(TFunc filter = default) where TFunc : unmanaged, ISplatFramebufferMask
        {
            if (!kernels.ContainsKey(filter.GetType()))
            {
                Action<Index1D, int, FrameBuffer, ArrayView1D<SplatData, Stride1D.Dense>, TFunc> kernel = device.LoadAutoGroupedStreamKernel<Index1D, int, FrameBuffer, ArrayView1D<SplatData, Stride1D.Dense>, TFunc>(Kernels.SplatFramebufferMaskKernel);
                kernels.Add(filter.GetType(), kernel);
            }

            return (Action<Index1D, int, FrameBuffer, ArrayView1D<SplatData, Stride1D.Dense>, TFunc>)kernels[filter.GetType()];
        }
    }
}
