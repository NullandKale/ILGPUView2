using Camera;
using ILGPU;
using ILGPU.Algorithms;
using ILGPU.Runtime;
using static GPU.Kernels;

namespace GPU
{
    public struct FillVoxels : IVoxelFramebufferFilter
    {
        public float skew;

        public FillVoxels(float skew)
        {
            this.skew = skew;
        }

        public void Apply(int tick, float x, float y, float z, dVoxels voxels, FrameBuffer images)
        {
            float eps = float.Epsilon * 10;

            float depth = images.GetDepthPixel(x, y, 2);
            depth = Utils.Remap(depth, 0, 0.001f, 1, 0);
            depth = 1f - depth;

            if(z <= depth)
            {
                voxels.Set(x, y, z, images.GetColorPixel(x, y), 1);
                //voxels.Set(x, y, z, new RGBA32(depth), 1);
            }
            else if (z <= eps && z >= -eps)
            {
                voxels.Set(x, y, z, images.GetColorPixel(x, y), 1);
                //voxels.Set(x, y, z, new RGBA32(depth), 1);
                return;
            }
            else
            {
                voxels.Set(x, y, z, new RGBA32(1, 0, 1, 0), 0);
            }
        }
    }

    public struct DebugDrawVoxels : IVoxelFilter
    {
        public float slider = 0.5f;
        public float skew = 0.5f;

        public DebugDrawVoxels(float slider, float skew)
        {
            this.slider = slider;
            this.skew = skew;
        }

        public RGBA32 Apply(int tick, float x, float y, dVoxels voxels, dImage output)
        {
            float range = 110f;
            float v = Utils.Remap(slider, 0, 1, range, -range);

            float stepX = 1f / output.width;
            float stepY = 1f / output.height;

            Vec3 center = new Vec3(v, 0, 350);
            Vec3 lookAt = new Vec3(v, 0, 0);

            Camera3D camera = new Camera3D(center, lookAt, new Vec3(0, -1, 0), output.width, output.height, 40f, new Vec3(1, 0, 1));
            var hit0 = voxels.hit(camera.GetRay(x + (stepX * 0.5f), y + (stepY * 0.5f)), 0, 5000, 2);
            var hit1 = voxels.hit(camera.GetRay(x, y), 0, 5000, 2);

            Vec3 color = (hit0.color + hit1.color) / 2.5f;

            return new RGBA32(color);
        }
    } 

    public class Voxels
    {
        public int xSize;
        public int ySize;
        public int zSize;

        private MemoryBuffer3D<RGBA32, Stride3D.DenseXY> colors;
        private MemoryBuffer3D<byte, Stride3D.DenseXY> states;
        private Device gpu;

        public Voxels(int xSize, int ySize, int zSize, Device gpu)
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

        public VoxelHit hit(Ray ray, float tmin, float tmax, int maxHitCount)
        {
            VoxelHit hit = new VoxelHit();

            //if (aabb.hit(ray, tmin, tmax))
            if (false)
                {
                Vec3 pos = ray.a;

                Vec3i iPos = new Vec3i(
                    XMath.Floor(pos.x),
                    XMath.Floor(pos.y),
                    XMath.Floor(pos.z));

                float stepAmount = 1;

                Vec3 step = new Vec3(
                    ray.b.x > 0 ? stepAmount : -stepAmount,
                    ray.b.y > 0 ? stepAmount : -stepAmount,
                    ray.b.z > 0 ? stepAmount : -stepAmount);

                Vec3 tDelta = new Vec3(
                    XMath.Abs(1f / ray.b.x),
                    XMath.Abs(1f / ray.b.y),
                    XMath.Abs(1f / ray.b.z));

                Vec3 dist = new Vec3(
                    step.x > 0 ? (iPos.x + 1 - pos.x) : (pos.x - iPos.x),
                    step.y > 0 ? (iPos.y + 1 - pos.y) : (pos.y - iPos.y),
                    step.z > 0 ? (iPos.z + 1 - pos.z) : (pos.z - iPos.z));

                Vec3 tMax = new Vec3(
                     float.IsInfinity(tDelta.x) ? float.MaxValue : tDelta.x * dist.x,
                     float.IsInfinity(tDelta.y) ? float.MaxValue : tDelta.y * dist.y,
                     float.IsInfinity(tDelta.z) ? float.MaxValue : tDelta.z * dist.z);

                int i = -1;
                int max = XMath.Max(xSize, ySize) * 3;

                while (i < max)
                {
                    Vec3 offsetPos = pos - aabb.min;

                    if ((offsetPos.x >= 0 && offsetPos.x < xSize) && (offsetPos.y >= 0 && offsetPos.y < ySize) && (offsetPos.z >= 0 && offsetPos.z < zSize))
                    {
                        RGBA32 tile = Get((int)offsetPos.x, (int)offsetPos.y, (int)offsetPos.z);
                        byte state = GetState((int)offsetPos.x, (int)offsetPos.y, (int)offsetPos.z);

                        if (state != 0)
                        {
                            if (hit.hitCount == 0)
                            {
                                hit.color = tile.toVec3();
                                hit.hitCount++;
                            }
                            else
                            {
                                hit.Hit(tile);
                                if (hit.hitCount > maxHitCount)
                                {
                                    return hit;
                                }
                            }
                        }
                    }

                    i++;

                    if (tMax.x < tMax.y)
                    {
                        if (tMax.x < tMax.z)
                        {
                            pos.x += step.x;
                            tMax.x += tDelta.x;
                        }
                        else
                        {
                            pos.z += step.z;
                            tMax.z += tDelta.z;
                        }
                    }
                    else
                    {
                        if (tMax.y < tMax.z)
                        {
                            pos.y += step.y;
                            tMax.y += tDelta.y;
                        }
                        else
                        {
                            pos.z += step.z;
                            tMax.z += tDelta.z;
                        }
                    }
                }

                return hit;
            }
            else
            {
                return hit;
            }
        }

        public RGBA32 Get(int x, int y, int z)
        {
            if (x >= 0 && x < xSize && y >= 0 && y < ySize && z >= 0 && z < zSize)
            {
                return colors[x, y, z];
            }
            else
            {
                return default(RGBA32);
            }
        }

        public byte GetState(int x, int y, int z)
        {
            if (x >= 0 && x < xSize && y >= 0 && y < ySize && z >= 0 && z < zSize)
            {
                return states[x, y, z];
            }
            else
            {
                return 0;
            }
        }

        public void Set(int x, int y, int z, RGBA32 color)
        {
            if (x >= 0 && x < xSize && y >= 0 && y < ySize && z >= 0 && z < zSize)
            {
                colors[x, y, z] = color;
            }
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

        public RGBA32 Get(float x, float y, RGBA32 def = default)
        {
            for (int i = zSize; i >= 0; i--)
            {
                float z = i / zSize;
                byte state = GetState(x, y, z);

                if (state != 0)
                {
                    return Get(x, y, z);
                }
            }

            return def;
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

    public struct VoxelHit
    {
        public Vec3 color;
        public byte state;
        public int hitCount;

        public VoxelHit()
        {
            color = default;
            state = 0;
            hitCount = 0;
        }

        public void Hit(RGBA32 v)
        {
            hitCount++;

            color.x += v.r / 255f;
            color.y += v.g / 255f;
            color.z += v.b / 255f;

            //color.x += v.r / 255f * (v.a / 255f);
            //color.y += v.g / 255f * (v.a / 255f);
            //color.z += v.b / 255f * (v.a / 255f);
        }

        public RGBA32 GetColormappedColor(bool useAces)
        {
            if (useAces)
            {
                color = Vec3.aces_approx(color / hitCount / 2f);
            }
            else
            {
                color = Vec3.reinhard(color / hitCount / 2f);
            }

            return new RGBA32(color);
        }

        public RGBA32 GetColor()
        {
            Vec3 toReturn = new Vec3(color);

            toReturn /= (float)hitCount;

            return new RGBA32(toReturn);
        }
    }
}
