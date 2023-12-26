using ILGPU.Runtime;
using ILGPU;
using GPU;
using static GPU.Kernels;
using System.Drawing;
using System;

namespace ILGPUView2.GPU.DataStructures
{
    public class GPUFrameBuffer : IDisposable
    {
        public int width;
        public int height;

        public bool dirty = false;
        public bool cpu_dirty = false;

        public int[] colorData;
        public float[] depthData;

        public MemoryBuffer1D<int, Stride1D.Dense>? gpuColorData;
        public MemoryBuffer1D<float, Stride1D.Dense>? gpuDepthData;

        public GPUFrameBuffer(int width, int height)
        {
            this.width = width;
            this.height = height;

            colorData = new int[width * height];
            depthData = new float[width * height];
        }

        public FrameBuffer toDevice(Renderer gpu)
        {
            if (gpuColorData == null || gpuColorData.Extent != colorData.Length)
            {
                if (colorData != null && colorData.Length > 0)
                {
                    gpuColorData = gpu.device.Allocate1D(colorData);
                }
                else
                {
                    gpuColorData = gpu.device.Allocate1D<int>(width * height);
                }
            }

            if (gpuDepthData == null || gpuDepthData.Extent != depthData.Length)
            {
                if (depthData != null && depthData.Length > 0)
                {
                    gpuDepthData = gpu.device.Allocate1D(depthData);
                }
                else
                {
                    gpuDepthData = gpu.device.Allocate1D<float>(width * height);
                }
            }

            if (cpu_dirty)
            {
                gpuColorData.CopyFromCPU(colorData);
                gpuDepthData.CopyFromCPU(depthData);
                cpu_dirty = false;
            }

            dirty = true;
            return new FrameBuffer(width, height, gpuColorData, gpuDepthData);
        }

        public (int[] colorData, float[] depthData) toCPU()
        {
            if (gpuColorData != null && gpuDepthData != null)
            {
                if (colorData == null || colorData.Length != gpuColorData.Length)
                {
                    colorData = new int[gpuColorData.Length];
                    dirty = true;
                }

                if (depthData == null || depthData.Length != gpuDepthData.Length)
                {
                    depthData = new float[gpuDepthData.Length];
                    dirty = true;
                }

                if (dirty)
                {
                    gpuColorData.CopyToCPU(colorData);
                    gpuDepthData.CopyToCPU(depthData);
                    dirty = false;
                }
            }

            return (colorData, depthData);
        }

        public Bitmap GetBitmap()
        {
            return Utils.BitmapFromBytes(colorData, width, height);
        }

        public void Dispose()
        {
            gpuColorData?.Dispose();
            gpuDepthData?.Dispose();
        }
    }


    public struct FrameBuffer
    {
        public int width;
        public int height;

        public ArrayView1D<int, Stride1D.Dense> color;
        public ArrayView1D<float, Stride1D.Dense> depth;

        public FrameBuffer(int width, int height,
                           ArrayView1D<int, Stride1D.Dense> color, ArrayView1D<float, Stride1D.Dense> depth)
        {
            this.width = width;
            this.height = height;
            this.color = color;
            this.depth = depth;
        }

        public void SetColorAt(int x, int y, RGBA32 toSet)
        {
            if (x >= 0 && x < width && y >= 0 && y < height)
            {
                int index = y * width + x;
                color[index] = toSet.ToInt();
            }
        }

        public void SetColorAt(float x, float y, RGBA32 toSet)
        {
            SetColorAt((int)(x * width), (int)(y * height), toSet);
        }

        public void SetColorAt(Vec2 pos, RGBA32 toSet)
        {
            SetColorAt(pos.x, pos.y, toSet);
        }

        public void SetColorAt(int index, RGBA32 toSet)
        {
            if (index >= 0 && index < this.color.Length)
            {
                color[index] = toSet.ToInt();
            }
        }

        public Vec3 GetColor(float x, float y)
        {
            var c = GetColorPixel((int)(x * width), (int)(y * height));

            return new Vec3(c.r / 255.0f, c.g / 255.0f, c.b / 255.0f);
        }

        public RGBA32 GetColorPixel(float x, float y)
        {
            return GetColorPixel((int)(x * width), (int)(y * height));
        }

        public RGBA32 GetColorPixel(int x, int y)
        {
            return GetColorPixel(y * width + x);
        }

        public RGBA32 GetColorPixel(int index)
        {
            if (index >= 0 && index < color.Length)
            {
                return new RGBA32(color[index]);
            }
            else
            {
                return new RGBA32(1, 0, 1, 0);
            }
        }

        public void SetDepthPixel(int xCord, int yCord, float val)
        {
            SetDepthPixel(yCord * width + xCord, val);
        }

        public void SetDepthPixel(int index, float val)
        {
            if (index >= 0 && index < depth.Length)
            {
                depth[index] = val;
            }
        }

        public float GetDepthPixel(int index)
        {
            if (index >= 0 && index < depth.Length)
            {
                return depth[index];
            }
            else
            {
                return 0.0f;
            }
        }

        public float GetDepthPixel(float xCord, float yCord)
        {
            return GetDepthPixel((int)((yCord * height) * width + (xCord * width)));
        }

        public float GetDepth(int xCord, int yCord)
        {
            return GetDepthPixel(yCord * width + xCord);
        }
    }

    public struct FrameBufferCopy : IFramebufferMask
    {
        public RGBA32 Apply(int tick, float x, float y, dImage output, FrameBuffer input)
        {
            return input.GetColorPixel(x, y);
        }
    }
}
