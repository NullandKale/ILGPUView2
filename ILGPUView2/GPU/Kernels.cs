using ILGPU.Runtime;
using ILGPU;
using GPU.RT;
using Camera;
using ILGPUView2.GPU;

namespace GPU
{
    public static class Kernels
    {
        public interface ISphereImageFilter
        {
            RGBA32 Apply(int tick, float x, float y, dImage output, ArrayView1D<Sphere, Stride1D.Dense> spheres);
        }

        public interface IBVHImageFilter
        {
            RGBA32 Apply(int tick, float x, float y, dImage output, DEVICE_BVH bvh);
        }

        public interface IImageFilter
        {
            RGBA32 Apply(int tick, float x, float y, dImage output);
        }

        public interface IImageMask
        {
            RGBA32 Apply(int tick, float x, float y, dImage output, dImage input);
        }

        public interface IFramebufferMask
        {
            RGBA32 Apply(int tick, float x, float y, dImage output, FrameBuffer input);
        }

        public interface IVoxelFramebufferFilter
        {
            void Apply(int tick, float x, float y, float z, dVoxels voxels, FrameBuffer images);
        }

        public interface IVoxelFilter
        {
            RGBA32 Apply(int tick, float x, float y, dVoxels voxels, dImage output);
        }

        public interface IParticleSystemFilter
        {
            RGBA32 Apply(int tick, float x, float y, dParticleSystem particles, dImage output);
        }

        public interface IParticleSystemUpdate
        {
            void Update(int tick, int id, dParticleSystem particles);
        }


        public static void ParticleSystemFilterKernel<TFunc>(Index1D index, int tick, dParticleSystem particles, dImage output, TFunc filter) where TFunc : unmanaged, IParticleSystemFilter
        {
            int x = index.X % output.width;
            int y = index.X / output.width;

            double u = (double)x / (double)output.width;
            double v = (double)y / (double)output.height;

            output.SetColorAt(x, y, filter.Apply(tick, (float)u, (float)v, particles, output));
        }

        public static void ParticleSystemUpdateKernel<TFunc>(Index1D index, int tick, dParticleSystem particles, TFunc filter) where TFunc : unmanaged, IParticleSystemUpdate
        {
            filter.Update(tick, index.X, particles);
        }

        public static void ImageFilterKernel<TFunc>(Index1D index, int tick, dImage output, TFunc filter) where TFunc : unmanaged, IImageFilter
        {
            int x = index.X % output.width;
            int y = index.X / output.width;

            double u = (double)x / (double)output.width;
            double v = (double)y / (double)output.height;

            output.SetColorAt(x, y, filter.Apply(tick, (float)u, (float)v, output));
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


        public static void SphereImageFilterKernel<TFunc>(Index1D index, int tick, dImage output, ArrayView1D<Sphere, Stride1D.Dense> spheres, TFunc filter) where TFunc : unmanaged, ISphereImageFilter
        {
            int x = index.X % output.width;
            int y = index.X / output.width;

            float u = (float)x / (float)output.width;
            float v = (float)y / (float)output.height;

            float min = 0.000001f;

            Vec2 uv = GetJitteredUV(tick, u, v, min, min);
            //Vec2 uv = new Vec2(u,v);

            output.SetColorAt(x, y, filter.Apply(tick, uv.x, uv.y, output, spheres));
        }

        public static void ImageMaskKernel<TFunc>(Index1D index, int tick, dImage output, dImage input, TFunc filter) where TFunc : unmanaged, IImageMask
        {
            int x = index.X % output.width;
            int y = index.X / output.width;

            double u = (double)x / (double)output.width;
            double v = (double)y / (double)output.height;

            output.SetColorAt(x, y, filter.Apply(tick, (float)u, (float)v, output, input));
        }

        public static void FramebufferMaskKernel<TFunc>(Index1D index, int tick, dImage output, FrameBuffer input, TFunc filter) where TFunc : unmanaged, IFramebufferMask
        {
            int x = index.X % output.width;
            int y = index.X / output.width;

            double u = (double)x / (double)output.width;
            double v = (double)y / (double)output.height;

            output.SetColorAt(x, y, filter.Apply(tick, (float)u, (float)v, output, input));
        }

        public static void VoxelFramebufferFilterKernel<TFunc>(Index2D index, int tick, dVoxels voxels, FrameBuffer images, TFunc filter) where TFunc : unmanaged, IVoxelFramebufferFilter
        {
            for (int i = 0; i < voxels.zSize; i++)
            {
                float z = i / (float)voxels.zSize;
                filter.Apply(tick, index.X / (float)voxels.xSize, index.Y / (float)voxels.ySize, z, voxels, images);
            }
        }

        public static void VoxelFilterKernel<TFunc>(Index1D index, int tick, dVoxels voxels, dImage output, TFunc filter) where TFunc : unmanaged, IVoxelFilter
        {
            int x = index.X % output.width;
            int y = index.X / output.width;

            double u = (double)x / (double)output.width;
            double v = (double)y / (double)output.height;

            output.SetColorAt(x, y, filter.Apply(tick, (float)u, (float)v, voxels, output));
        }
    }
}
