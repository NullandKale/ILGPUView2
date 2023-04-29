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
        public bool cpu_dirty = false;

        public int[] data;

        public MemoryBuffer1D<int, Stride1D.Dense>? gpuData;

        public GPUImage(int width, int height)
        {
            this.width = width;
            this.height = height;

            data = new int[width * height];
        }

        public GPUImage(Bitmap b)
        {
            this.width = b.Width;
            this.height = b.Height;
            byte[] data_bytes = Utils.BitmapToBytes(b);
            data = new int[width * height];
            Buffer.BlockCopy(data_bytes, 0, data, 0, data_bytes.Length);
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
                    gpuData = gpu.device.Allocate1D<int>(width * height);
                }
            }

            if(cpu_dirty)
            {
                gpuData.CopyFromCPU(data);
                cpu_dirty = false;
            }

            dirty = true;
            return new dImage(width, height, gpuData);
        }

        public ref int[] toCPU()
        {
            if(gpuData != null) 
            {
                if(data == null || data.Length != gpuData.Length)
                {
                    data = new int[gpuData.Length];
                    dirty = true;
                }

                if(dirty)
                {
                    gpuData.CopyToCPU(data);
                    dirty = false;
                }
            }

            return ref data;
        }

        public Bitmap GetBitmap()
        {
            byte[] data_bytes = new byte[width * height * 4];
            Buffer.BlockCopy(data, 0, data_bytes, 0, data_bytes.Length);
            return Utils.BitmapFromBytes(data_bytes, width, height);
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

        public ArrayView1D<int, Stride1D.Dense> data;

        public dImage(int width, int height, MemoryBuffer1D<int, Stride1D.Dense> data)
        {
            this.width = width;
            this.height = height;
            this.data = data;
        }

        public RGBA32 GetColorAt(int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
            {
                return new RGBA32();
            }

            int index = (y * width + x);
            return new RGBA32(data[index]);
        }

        public void SetColorAt(int x, int y, int color)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
            {
                return;
            }

            int index = (y * width + x);
            data[index] = color;
        }

        public void SetColorAt(int x, int y, RGBA32 color)
        {
            SetColorAt(x, y, color.ToInt());
        }

        public RGBA32 GetColorAt(float x, float y)
        {
            int x_idx = (int)(x * width);
            int y_idx = (int)(y * height);

            return GetColorAt(x_idx, y_idx);
        }

        public Vec3 GetPixel(float x, float y)
        {
            int x_idx = (int)(x * width);
            int y_idx = (int)(y * height);

            return new Vec3(GetColorAt(x_idx, y_idx));
        }
    }
}
