using ILGPU.Runtime;
using ILGPU;
using GPU.RT;
using Camera;
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
    }
}
