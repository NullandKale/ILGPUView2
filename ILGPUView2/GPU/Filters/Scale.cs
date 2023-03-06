using GPU;
using static GPU.Kernels;

namespace ILGPUView2.GPU.Filters
{
    public struct Scale : IImageMask
    {
        public RGBA32 Apply(int tick, float x, float y, dImage framebuffer, dImage input)
        {
            return input.GetColorAt(x, y);
        }
    }
}