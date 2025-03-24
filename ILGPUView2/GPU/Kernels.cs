using ILGPU.Runtime;
using ILGPU;
using GPU.RT;
using ILGPUView2.GPU;
using ILGPUView2.GPU.RT;
using ILGPUView2.GPU.DataStructures;
using System.Runtime.CompilerServices;
using ILGPUView2.GPU.Filters;

namespace GPU
{
    public static partial class Kernels
    {
        public static void ImageToRGB(Index1D index, ArrayView1D<byte, Stride1D.Dense> output, dImage input)
        {
            int x = index.X % input.width;
            int y = index.X / input.width;

            RGBA32 color = input.GetColorAt(x, y);
            
            output[index * 3 + 0] = color.r;
            output[index * 3 + 1] = color.g;
            output[index * 3 + 2] = color.b;
        }

        public static void RGBToImage(Index1D index, dImage output, ArrayView1D<byte, Stride1D.Dense> input)
        {
            int x = index.X % output.width;
            int y = index.X / output.width;

            RGBA32 color = new RGBA32(0, 0, 0, 255);

            color.r = input[index * 3 + 0];
            color.g = input[index * 3 + 1];
            color.b = input[index * 3 + 2]; 

            output.SetColorAt(x, y, color);
        }

        private static float GenerateRandomValue(int sequenceX, int sequenceY, int tick)
        {
            // Re-seed the random number generator with the sequence index to ensure repeatable results for each index
            int seed = sequenceX * 32 + sequenceY;

            // Shuffle the random number generator's internal state to add additional randomness
            for (int i = 0; i < tick % 10; i++)
            {
                seed = (seed * 1103515245 + 12345) % 2147483647;
            }

            // Use the re-seeded and shuffled random number generator to generate a random value for the given sequence index
            double randomValue = ((sequenceX + 1) * (sequenceY + 1) * seed) % 1000000.0 / 1000000.0;

            return (float)randomValue;
        }

        private static Vec2 GetJitteredUV(int tick, float u, float v, float uMin, float vMin)
        {
            // Define the dimensions of the sequence
            const int sequenceWidth = 32;
            const int sequenceHeight = 32;

            // Calculate the current index within the sequence
            int sequenceX = (int)(u * sequenceWidth);
            int sequenceY = (int)(v * sequenceHeight);

            // Generate a random value for the given index
            float randomValue = GenerateRandomValue(sequenceX, sequenceY, tick);

            // Calculate the jittered u and v values
            float jitteredU = u + uMin * randomValue;
            float jitteredV = v + vMin * randomValue;

            // Return the jittered u and v values as a Vec2
            return new Vec2(jitteredU, jitteredV);
        }

        /// <summary>
        /// Kernel to scale an input image (in RGBA) to a target resolution and convert to an array of RGB floats.
        /// </summary>
        public static void ImageToRGBFloats(
            Index1D index,
            dImage input,
            ArrayView<float> output,
            int outWidth,
            int outHeight)
        {
            int totalPixels = outWidth * outHeight;
            if (index >= totalPixels)
                return;

            // Compute output pixel coordinates
            int x = index % outWidth;
            int y = index / outWidth;

            // Compute normalized coordinates (center of pixel)
            float u = (x + 0.5f) / outWidth;
            float v = (y + 0.5f) / outHeight;

            // Map to input image coordinates
            float inX = u * input.width;
            float inY = v * input.height;

            // Use nearest-neighbor sampling
            int srcX = (int)inX;
            int srcY = (int)inY;
            if (srcX >= input.width)
                srcX = input.width - 1;
            if (srcY >= input.height)
                srcY = input.height - 1;

            // Get the color from the input image (assumes dImage.GetColorAt is available on GPU)
            RGBA32 color = input.GetColorAt(srcX, srcY);

            // Write normalized RGB values into the output float array
            int outIndex = index * 3;
            output[outIndex + 0] = color.r / 255.0f;
            output[outIndex + 1] = color.g / 255.0f;
            output[outIndex + 2] = color.b / 255.0f;
        }

        /// <summary>
        /// Converts a single-channel depth float value to a BGRA pixel.
        /// For each pixel, applies: scaled = depth * alpha + beta, clamps to [0,255],
        /// then writes the same grayscale value into B, G, and R, with A = 255.
        /// </summary>
        public static void DepthFloatsToBGRAImage(
            Index1D index,
            ArrayView<float> depthInput,
            dImage output,
            float alpha,
            float beta)
        {
            int totalPixels = output.width * output.height;
            if (index >= totalPixels)
                return;

            float depthVal = depthInput[index];
            float scaled = depthVal * alpha + beta;

            // Clamp the value between 0 and 255
            scaled = scaled < 0f ? 0f : (scaled > 255f ? 255f : scaled);
            byte gray = (byte)scaled;

            // Create a BGRA pixel where B, G, and R are the same grayscale value and A is 255.
            RGBA32 color = new RGBA32(gray, gray, gray, 255);

            int x = index % output.width;
            int y = index / output.width;
            output.SetColorAt(x, y, color);
        }
    }
}
