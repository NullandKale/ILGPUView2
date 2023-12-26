using ILGPU;
using ILGPU.Algorithms;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using System.Numerics;
using static GPU.Kernels;

namespace GPU
{
    public struct FillVoxels : IVoxelMask
    {
        public FillVoxels()
        {

        }

        public void Apply(int tick, float x, float y, float z, dVoxels voxels, dImage depthImage, dImage colorImage)
        {
            (RGBA32 color, byte occupancy) = GetValueFromTextures(x, y, z, depthImage, colorImage);

            voxels.Set(x, y, z, color, occupancy);
        }

        public (RGBA32, byte) GetValueFromTextures(float x, float y, float z, dImage depthImage, dImage colorImage)
        {
            float depth = depthImage.GetColorAt(x, y).g / 255.0f;
            RGBA32 color;
            byte occupancy;

            if (z <= depth)
            {
                color = colorImage.GetColorAt(x, y);
                occupancy = 1;
            }
            else
            {
                color = new RGBA32(1, 0, 1, 0);
                occupancy = 0;
            }

            return (color, occupancy);
        }
    }


    public struct dVoxels
    {
        public int xSize;
        public int ySize;
        public int zSize;
        public Vec3 size;
        public AABB aabb;

        public ArrayView3D<RGBA32, Stride3D.DenseXY> colors;
        public ArrayView3D<byte, Stride3D.DenseXY> states;

        public dVoxels(int xSize, int ySize, int zSize, MemoryBuffer3D<RGBA32, Stride3D.DenseXY> colors, MemoryBuffer3D<byte, Stride3D.DenseXY> states)
        {
            this.xSize = xSize;
            this.ySize = ySize;
            this.zSize = zSize;
            this.colors = colors;
            this.states = states;
            aabb = new AABB(new Vec3(-xSize / 2f, -ySize / 2f, -zSize / 2f), new Vec3(xSize / 2f, ySize / 2f, zSize / 2f));
        }

        public VoxelHit hit(Ray ray, int max)
        {
            VoxelHit hit = new VoxelHit();

            Vec3 pos = ray.a;

            Vec3i iPos = new Vec3i(
                XMath.Floor(pos.x),
                XMath.Floor(pos.y),
                XMath.Floor(pos.z));

            Vec3 tDelta = new Vec3(
                XMath.Abs(1f / ray.b.x),
                XMath.Abs(1f / ray.b.y),
                XMath.Abs(1f / ray.b.z));

            Vec3 dist = new Vec3(
                ray.b.x > 0 ? (iPos.x + 1 - pos.x) : (pos.x - iPos.x),
                ray.b.y > 0 ? (iPos.y + 1 - pos.y) : (pos.y - iPos.y),
                ray.b.z > 0 ? (iPos.z + 1 - pos.z) : (pos.z - iPos.z));

            Vec3 tMax = new Vec3(
                 float.IsInfinity(tDelta.x) ? float.MaxValue : tDelta.x * dist.x,
                 float.IsInfinity(tDelta.y) ? float.MaxValue : tDelta.y * dist.y,
                 float.IsInfinity(tDelta.z) ? float.MaxValue : tDelta.z * dist.z);

            int i = -1;

            while (i < max)
            {
                Vec3 offsetPos = pos - aabb.min;

                if ((offsetPos.x >= 0 && offsetPos.x < xSize) && (offsetPos.y >= 0 && offsetPos.y < ySize) && (offsetPos.z >= 0 && offsetPos.z < zSize))
                {
                    byte state = states[(int)offsetPos.x, (int)offsetPos.y, (int)offsetPos.z];

                    if (state != 0)
                    {
                        hit.color = colors[(int)offsetPos.x, (int)offsetPos.y, (int)offsetPos.z].toVec3();
                        hit.hitCount = 1;
                        return hit;
                    }
                }

                i++;

                float tNext;
                if (tMax.x < tMax.y)
                {
                    if (tMax.x < tMax.z)
                    {
                        tNext = tMax.x;
                        tMax.x += tDelta.x;
                    }
                    else
                    {
                        tNext = tMax.z;
                        tMax.z += tDelta.z;
                    }
                }
                else
                {
                    if (tMax.y < tMax.z)
                    {
                        tNext = tMax.y;
                        tMax.y += tDelta.y;
                    }
                    else
                    {
                        tNext = tMax.z;
                        tMax.z += tDelta.z;
                    }
                }
                pos = ray.a + ray.b * tNext;

            }

            return hit;
        }

        public RGBA32 Get(float x, float y, float z)
        {
            int xIndex = (int)(x * xSize);
            int yIndex = (int)(y * ySize);
            int zIndex = (int)(z * zSize);

            if (xIndex >= 0 && xIndex < xSize && yIndex >= 0 && yIndex < ySize && zIndex >= 0 && zIndex < zSize)
            {
                return colors[xIndex, yIndex, zIndex];
            }
            else
            {
                return default(RGBA32);
            }
        }

        public byte GetState(float x, float y, float z)
        {
            int xIndex = (int)(x * xSize);
            int yIndex = (int)(y * ySize);
            int zIndex = (int)(z * zSize);

            if (xIndex >= 0 && xIndex < xSize && yIndex >= 0 && yIndex < ySize && zIndex >= 0 && zIndex < zSize)
            {
                return states[xIndex, yIndex, zIndex];
            }
            else
            {
                return 0;
            }
        }

        public void Set(float x, float y, float z, RGBA32 color, byte state = 0)
        {
            int xIndex = (int)(x * xSize);
            int yIndex = (int)(y * ySize);
            int zIndex = (int)(z * zSize);

            if (xIndex >= 0 && xIndex < xSize && yIndex >= 0 && yIndex < ySize && zIndex >= 0 && zIndex < zSize)
            {
                colors[xIndex, yIndex, zIndex] = color;
                states[xIndex, yIndex, zIndex] = state;
            }
        }
    }


    public class Voxels
    {
        public int xSize;
        public int ySize;
        public int zSize;

        private MemoryBuffer3D<RGBA32, Stride3D.DenseXY> colors;
        private MemoryBuffer3D<byte, Stride3D.DenseXY> states;
        private Renderer gpu;

        public Voxels(int xSize, int ySize, int zSize, Renderer gpu)
        {
            this.xSize = xSize;
            this.ySize = ySize;
            this.zSize = zSize;
            this.gpu = gpu;

            colors = gpu.device.Allocate3DDenseXY<RGBA32>(new LongIndex3D(xSize, ySize, zSize));
            states = gpu.device.Allocate3DDenseXY<byte>(new LongIndex3D(xSize, ySize, zSize));
        }

        internal dVoxels toDevice()
        {
            return new dVoxels(xSize, ySize, zSize, colors, states);
        }
    }


    public struct VoxelHit
    {
        public Vec3 color;
        public int hitCount;

        public VoxelHit()
        {
            color = default;
            hitCount = 0;
        }
    }
}
