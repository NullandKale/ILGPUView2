using Camera;
using GPU;
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
    public interface IFramebufferMask
    {
        RGBA32 Apply(int tick, float x, float y, dImage output, FrameBuffer input);
    }

    public static partial class Kernels
    {
        public static void FramebufferMaskKernel<TFunc>(Index1D index, int tick, dImage output, FrameBuffer input, TFunc filter) where TFunc : unmanaged, IFramebufferMask
        {
            int x = index.X % output.width;
            int y = index.X / output.width;

            double u = (double)x / (double)output.width;
            double v = (double)y / (double)output.height;

            output.SetColorAt(x, y, filter.Apply(tick, (float)u, (float)v, output, input));
        }
    }

    public partial class Device
    {
        public void ExecuteFramebufferMask<TFunc>(GPUImage output, FrameBuffer input, TFunc filter = default) where TFunc : unmanaged, IFramebufferMask
        {
            var kernel = GetFramebufferMaskKernel(filter);
            kernel(output.width * output.height, ticks, output.toDevice(this), input, filter);
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
    }
}
