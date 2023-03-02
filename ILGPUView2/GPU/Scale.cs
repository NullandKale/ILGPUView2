using static GPU.Kernels;

namespace GPU
{
    public struct Scale : IImageMask
    {
        public RGBA32 Apply(int tick, float x, float y, dImage framebuffer, dImage input)
        {
            return input.GetColorAt(x, y);
        }
    }
}