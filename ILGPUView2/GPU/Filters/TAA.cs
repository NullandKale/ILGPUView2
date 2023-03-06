using GPU;
using static GPU.Kernels;

namespace ILGPUView2.GPU.Filters
{
    public struct TAA : IImageMask
    {
        // default to 0.5f
        public float rate = 0.5f;

        public TAA(float rate)
        {
            this.rate = rate;
        }

        public RGBA32 Apply(int tick, float x, float y, dImage output, dImage input)
        {
            // Get the color values of the previous and new frames at the given pixel coordinates
            Vec3 previousColor = output.GetPixel(x, y);
            Vec3 newColor = input.GetPixel(x, y);

            // Blend the new frame color with the previous frame color
            Vec3 blendedColor = Vec3.lerp(previousColor, newColor, rate);

            // Return the blended color as an RGBA32 value
            return new RGBA32(blendedColor);
        }
    }
}
