using ILGPU;
using ILGPU.Algorithms;
using ILGPU.Runtime;
using System;
using System.Drawing;
using System.IO;
using static GPU.Kernels;

namespace GPU
{
    public class GPUImage : IDisposable
    {
        public int width;
        public int height;
        
        public bool dirty = false;

        public byte[] data;

        public MemoryBuffer1D<byte, Stride1D.Dense>? gpuData;

        public GPUImage(int width, int height)
        {
            this.width = width;
            this.height = height;

            data = new byte[width * height * 4];
        }

        public GPUImage(Bitmap b)
        {
            this.width = b.Width;
            this.height = b.Height;

            data = Utils.BitmapToBytes(b);
        }

        public static bool TryLoad(string file, out GPUImage image)
        {
            string ext = Path.GetExtension(file);

            if (File.Exists(file) && ext.Length > 2)
            {
                Bitmap bmp;
                try
                {
                    bmp = new Bitmap(file);
                }
                catch (Exception)
                {
                    image = null;
                    return false;
                }
                image = new GPUImage(bmp);
                bmp.Dispose();

                return true;
            }

            image = null;
            return false;
        }

        public dImage toDevice(Device gpu)
        {
            if(gpuData == null)
            {
                if(data != null && data.Length > 0)
                {
                    gpuData = gpu.device.Allocate1D(data);
                }
                else
                {
                    gpuData = gpu.device.Allocate1D<byte>(width * height * 4);
                }
            }

            dirty = true;
            return new dImage(width, height, gpuData);
        }

        public byte[] toCPU()
        {
            if(gpuData != null) 
            {
                if(data == null || data.Length != gpuData.Length)
                {
                    data = new byte[gpuData.Length];
                    dirty = true;
                }

                if(dirty)
                {
                    gpuData.CopyToCPU(data);
                    dirty = false;
                }
            }

            return data;
        }

        public Bitmap GetBitmap()
        {
            return Utils.BitmapFromBytes(data, width, height);
        }

        public void Dispose()
        {
            gpuData?.Dispose();
        }
    }

    public struct dImage
    {
        public int width;
        public int height;

        public ArrayView1D<byte, Stride1D.Dense> data;

        public dImage(int width, int height, MemoryBuffer1D<byte, Stride1D.Dense> data)
        {
            this.width = width;
            this.height = height;
            this.data = data;
        }

        public RGBA32 GetColorAt(int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
            {
                return default;
            }

            int index = (y * width + x) * 4;
            byte r = data[index + 0];
            byte g = data[index + 1];
            byte b = data[index + 2];
            byte a = data[index + 3];

            return new RGBA32(r, g, b, a);
        }

        public void SetColorAt(int x, int y, byte r, byte g, byte b, byte a)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
            {
                return;
            }

            int index = (y * width + x) * 4;
            data[index + 0] = r;
            data[index + 1] = g;
            data[index + 2] = b;
            data[index + 3] = a;
        }

        public void AddColorAt(int x, int y, Vec3 color, float depth)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
            {
                return;
            }

            int index = (y * width + x) * 4;

            byte a = data[index + 3];

            byte depthValue = (byte)(depth * 255.0f);

            if (depthValue < a)
            {
                byte r = (byte)XMath.Clamp(color.x * 255.0f, 0, 255);
                byte g = (byte)XMath.Clamp(color.y * 255.0f, 0, 255);
                byte b = (byte)XMath.Clamp(color.z * 255.0f, 0, 255);
                a = depthValue;

                data[index + 0] = r;
                data[index + 1] = g;
                data[index + 2] = b;
                data[index + 3] = a;
            }
        }

        public void SetColorAt(int x, int y, RGBA32 color)
        {
            SetColorAt(x, y, color.r, color.g, color.b, color.a);
        }

        public RGBA32 GetColorAt(float x, float y)
        {
            if (x < 0.0f || x >= 1.0f || y < 0.0f || y >= 1.0f)
            {
                return default;
            }

            int x_idx = (int)(x * width);
            int y_idx = (int)(y * height);

            return GetColorAt(x_idx, y_idx);
        }

        public Vec3 GetPixel(float x, float y)
        {
            if (x < 0.0f || x >= 1.0f || y < 0.0f || y >= 1.0f)
            {
                return default;
            }

            int x_idx = (int)(x * width);
            int y_idx = (int)(y * height);

            return new Vec3(GetColorAt(x_idx, y_idx));
        }

        public void SetColorAt(float x, float y, byte r, byte g, byte b, byte a)
        {
            if (x < 0.0f || x >= 1.0f || y < 0.0f || y >= 1.0f)
            {
                return;
            }

            int x_idx = (int)(x * width);
            int y_idx = (int)(y * height);

            SetColorAt(x_idx, y_idx, r, g, b, a);
        }

        public void SetColorAt(float x, float y, RGBA32 color)
        {
            SetColorAt(x, y, color.r, color.g, color.b, color.a);
        }
    }
}
