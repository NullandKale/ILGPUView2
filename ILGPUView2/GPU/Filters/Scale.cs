using GPU;
using ILGPU.Algorithms;
using System;
using static GPU.Kernels;

namespace ILGPUView2.GPU.Filters
{
    public struct Scale : IImageMask
    {
        public RGBA32 Apply(int tick, float u, float v, dImage output, dImage input)
        {
            return input.GetColorAt(u, v);
        }
    }

    public struct AverageScale : IImageMask
    {
        public RGBA32 Apply(int tick, float u, float v, dImage output, dImage input)
        {
            // Calculate the corresponding region in the input image
            float inputUStart = u * input.width;
            float inputVStart = v * input.height;
            float inputUEnd = (u + 1.0f / output.width) * input.width;
            float inputVEnd = (v + 1.0f / output.height) * input.height;

            Vec3 totalColor = new Vec3(0, 0, 0);
            int sampleCount = 0;

            // Iterate over the pixels in the corresponding region
            for (int iy = (int)inputVStart; iy < inputVEnd; iy++)
            {
                for (int ix = (int)inputUStart; ix < inputUEnd; ix++)
                {
                    // Add the color of each input pixel
                    Vec3 pixelColor = input.GetPixel(ix, iy);
                    totalColor += pixelColor;
                    sampleCount++;
                }
            }

            if (sampleCount > 0)
            {
                // Calculate the average color
                Vec3 avgColor = totalColor / sampleCount;

                return new RGBA32(avgColor.x, avgColor.y, avgColor.z);
            }
            else
            {
                // Return black if no samples were taken
                return new RGBA32(0, 0, 0);
            }
        }
    }



    public unsafe struct ScaleLanczos : IImageMask
    {
        private float sigma = 1.0f;
        private int kernelSize = 4;
        public const int maxKernelSize = 11;

        private fixed float kernel[maxKernelSize * maxKernelSize];

        public ScaleLanczos()
        {
            float[] data = CreateLanczosKernel(kernelSize);

            for (int i = 0; i < data.Length; i++)
            {
                kernel[i] = data[i];
            }
        }

        public ScaleLanczos(float sigma, int kernelSize)
        {
            this.sigma = sigma;
            this.kernelSize = kernelSize;

            float[] data = CreateLanczosKernel(kernelSize);

            for (int i = 0; i < data.Length; i++)
            {
                kernel[i] = data[i];
            }

        }

        private static float[] CreateLanczosKernel(int size, int lobes = 2)
        {
            float[] kernel = new float[maxKernelSize * maxKernelSize];

            int halfSize = size / 2;
            for (int y = -halfSize; y <= halfSize; y++)
            {
                for (int x = -halfSize; x <= halfSize; x++)
                {
                    float r = (float)Math.Sqrt(x * x + y * y);
                    kernel[((y + halfSize) * size) + (x + halfSize)] = r < halfSize ? Sinc(r) * Sinc(r / lobes) : 0;
                }
            }

            return kernel;
        }

        private static float Sinc(float x)
        {
            return x != 0 ? (float)(Math.Sin(Math.PI * x) / (Math.PI * x)) : 1;
        }

        public unsafe RGBA32 Apply(int tick, float x, float y, dImage output, dImage input)
        {
            // Convert x, y (in 0 - 1 space) to corresponding input image coordinates
            float inputX = x * (input.width - 1);
            float inputY = y * (input.height - 1);

            // Get integer and fractional parts of coordinates
            int x1 = (int)XMath.Floor(inputX);
            int y1 = (int)XMath.Floor(inputY);

            // Ensure the second coordinates are within bounds
            int x2 = XMath.Min(x1 + kernelSize - 1, input.width - 1);
            int y2 = XMath.Min(y1 + kernelSize - 1, input.height - 1);

            // Initialize a color accumulator
            Vec3 colorSum = new Vec3(0, 0, 0);
            float weightSum = 0;

            // Apply the Lanczos kernel
            int halfKernelSize = kernelSize / 2;
            for (int ky = y1 - halfKernelSize; ky <= y2 + halfKernelSize; ky++)
            {
                for (int kx = x1 - halfKernelSize; kx <= x2 + halfKernelSize; kx++)
                {
                    // Clamp the coordinates to the image dimensions
                    int clampedX = XMath.Clamp(kx, 0, input.width - 1);
                    int clampedY = XMath.Clamp(ky, 0, input.height - 1);

                    // Fetch the kernel weight
                    float weight = kernel[(ky - y1 + halfKernelSize) * kernelSize + (kx - x1 + halfKernelSize)];

                    // Fetch the pixel color in the 0 - 1 space
                    Vec3 pixelColor = input.GetPixel(clampedX / (float)(input.width - 1), clampedY / (float)(input.height - 1));

                    // Apply the weight to the pixel color and accumulate the result
                    colorSum += weight * pixelColor;
                    weightSum += weight;
                }
            }

            // Normalize the color sum by the weight sum to get the final color
            Vec3 finalColor = colorSum / weightSum;

            return new RGBA32(finalColor.x, finalColor.y, finalColor.z);
        }

    }

}