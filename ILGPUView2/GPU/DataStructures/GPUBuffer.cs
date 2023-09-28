using ILGPU.Runtime;
using ILGPU;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPU;
using ILGPU.Algorithms;

namespace ILGPUView2.GPU.DataStructures
{
    public class GPUBuffer<T> where T : unmanaged
    {
        private readonly Accelerator gpu;
        public readonly long size;
        private readonly int dims;

        private readonly MemoryBuffer1D<int, Stride1D.Dense> dimensionSizes;
        private readonly int[] dimensionSizesData;
        
        private readonly MemoryBuffer1D<int, Stride1D.Dense> dimensionLengths;
        private readonly int[] dimensionLengthsData;
        
        private MemoryBuffer1D<T, Stride1D.Dense> data;
        private T[] cpuData;

        private bool isDirty = true;

        public GPUBuffer(Accelerator gpu, int x = 0, int y = 0, int z = 0, int w = 0, int u = 0, int v = 0)
        {
            this.gpu = gpu;
            dimensionSizesData = new int[] { x, y, z, w, u, v };
            dimensionLengthsData = GetDimensionLengths(x, y, z, w, u, v);
            
            dims = dimensionSizesData.Count(size => size > 0);

            size = dimensionSizesData[0];
            for (int i = 1; i < dimensionSizesData.Length; i++)
            {
                size *= dimensionSizesData[i] == 0 ? 1 : dimensionSizesData[i];
            }

            dimensionSizes = gpu.Allocate1D<int, Stride1D.Dense>(dimensionSizesData.Length, new Stride1D.Dense());
            dimensionLengths = gpu.Allocate1D<int, Stride1D.Dense>(dimensionLengthsData.Length, new Stride1D.Dense());
            data = gpu.Allocate1D<T, Stride1D.Dense>(size, new Stride1D.Dense());

            dimensionSizes.CopyFromCPU(dimensionSizesData);
            dimensionLengths.CopyFromCPU(dimensionLengthsData);

            cpuData = new T[size];
        }

        public dBuffer<T> toGPU()
        {
            if(isDirty)
            {
                isDirty = false;
                data.CopyFromCPU(cpuData);
            }

            return new dBuffer<T>(dims, data, dimensionSizes, dimensionLengths);
        }

        public void toCPU()
        {
            data.CopyToCPU(cpuData);
            dimensionSizes.CopyToCPU(dimensionSizesData);
            dimensionLengths.CopyToCPU(dimensionLengthsData);
        }

        public T Get(int x = 0, int y = 0, int z = 0, int w = 0, int u = 0, int v = 0)
        {
            return cpuData[GetIndex(x, y, z, w, u, v)];
        }

        public void Set(T value = default, int x = 0, int y = 0, int z = 0, int w = 0, int u = 0, int v = 0)
        {
            isDirty = true;
            long index = GetIndex(x, y, z, w, u, v);
            cpuData[index] = value;
        }

        public T Get(float x = 0, float y = 0, float z = 0, float w = 0, float u = 0, float v = 0)
        {
            return cpuData[GetIndex(x, y, z, w, u, v)];
        }

        public void Set(T value = default, float x = 0, float y = 0, float z = 0, float w = 0, float u = 0, float v = 0)
        {
            cpuData[GetIndex(x, y, z, w, u, v)] = value;
        }

        public long GetIndex(int x = 0, int y = 0, int z = 0, int w = 0, int u = 0, int v = 0)
        {
            x = dimensionSizesData[0] == 0 ? 0 : XMath.Abs(x) % dimensionSizesData[0]; 
            y = dimensionSizesData[1] == 0 ? 0 : XMath.Abs(y) % dimensionSizesData[1]; 
            z = dimensionSizesData[2] == 0 ? 0 : XMath.Abs(z) % dimensionSizesData[2];
            w = dimensionSizesData[3] == 0 ? 0 : XMath.Abs(w) % dimensionSizesData[3]; 
            u = dimensionSizesData[4] == 0 ? 0 : XMath.Abs(u) % dimensionSizesData[4]; 
            v = dimensionSizesData[5] == 0 ? 0 : XMath.Abs(v) % dimensionSizesData[5];

            return x * dimensionLengthsData[1] * dimensionLengthsData[2] * dimensionLengthsData[3] * dimensionLengthsData[4] * dimensionLengthsData[5] + 
                   y * dimensionLengthsData[2] * dimensionLengthsData[3] * dimensionLengthsData[4] * dimensionLengthsData[5] + 
                   z * dimensionLengthsData[3] * dimensionLengthsData[4] * dimensionLengthsData[5] + 
                   w * dimensionLengthsData[4] * dimensionLengthsData[5] + 
                   u * dimensionLengthsData[5] + 
                   v;
        }

        public long GetIndex(float x = 0f, float y = 0f, float z = 0f, float w = 0f, float u = 0f, float v = 0f)
        {
            x = XMath.Abs(x) % 1.0f; y = XMath.Abs(y) % 1.0f; z = XMath.Abs(z) % 1.0f;
            w = XMath.Abs(w) % 1.0f; u = XMath.Abs(u) % 1.0f; v = XMath.Abs(v) % 1.0f;

            return (long)(x * dimensionLengthsData[1] * dimensionLengthsData[2] * dimensionLengthsData[3] * dimensionLengthsData[4] * dimensionLengthsData[5] +
                   y * dimensionLengthsData[2] * dimensionLengthsData[3] * dimensionLengthsData[4] * dimensionLengthsData[5] +
                   z * dimensionLengthsData[3] * dimensionLengthsData[4] * dimensionLengthsData[5] +
                   w * dimensionLengthsData[4] * dimensionLengthsData[5] +
                   u * dimensionLengthsData[5] +
                   v);
        }

        private static int[] GetDimensionLengths(int x = 0, int y = 0, int z = 0, int w = 0, int v = 0, int u = 0)
        {
            List<int> dimensionSizes = new List<int>() { x, y, z, w, v, u };

            if (dimensionSizes.Any(size => size < 0))
            {
                throw new ArgumentException("Dimension sizes must be non-negative.");
            }

            int[] dimensionLengths = new int[6];

            int length = 1;

            for (int i = dimensionSizes.Count - 1; i >= 0; i--)
            {
                dimensionLengths[i] = length;
                length *= dimensionSizes[i];
            }

            return dimensionLengths;
        }
    }

    public struct dBuffer<T> where T : unmanaged
    {
        private long size;
        public int dims;
        private ArrayView1D<int, Stride1D.Dense> dimensionSizes;
        private ArrayView1D<int, Stride1D.Dense> dimensionLengths;
        public ArrayView1D<T, Stride1D.Dense> data;

        public dBuffer(int dims,
                       MemoryBuffer1D<T, Stride1D.Dense> data, 
                       MemoryBuffer1D<int, Stride1D.Dense> dimensionSizes, 
                       MemoryBuffer1D<int, Stride1D.Dense> dimensionLengths)
        {
            this.size = data.Length;
            this.dims = dims;
            this.data = data;
            this.dimensionSizes = dimensionSizes;
            this.dimensionLengths = dimensionLengths;
        }

        public T Get(int x = 0, int y = 0, int z = 0, int w = 0, int u = 0, int v = 0)
        {
            return data[GetIndex(x, y, z, w, u, v)];
        }

        public void Set(T value = default, int x = 0, int y = 0, int z = 0, int w = 0, int u = 0, int v = 0)
        {
            data[GetIndex(x, y, z, w, u, v)] = value;
        }

        public T Get(float x = 0, float y = 0, float z = 0, float w = 0, float u = 0, float v = 0)
        {
            return data[GetIndex(x, y, z, w, u, v)];
        }

        public void Set(T value = default, float x = 0, float y = 0, float z = 0, float w = 0, float u = 0, float v = 0)
        {
            data[GetIndex(x, y, z, w, u, v)] = value;
        }

        public int GetDims()
        {
            return dims;
        }

        public int GetDimSize(int dim)
        {
            if (dim < 0 || dim >= dimensionSizes.Length)
            {
                return 0;
            }

            return dimensionLengths[dim];
        }

        public long GetIndex(int x = 0, int y = 0, int z = 0, int w = 0, int u = 0, int v = 0)
        {
            x = XMath.Abs(x) % dimensionSizes[0]; y = XMath.Abs(y) % dimensionSizes[1]; z = XMath.Abs(z) % dimensionSizes[2];
            w = XMath.Abs(w) % dimensionSizes[3]; u = XMath.Abs(u) % dimensionSizes[4]; v = XMath.Abs(v) % dimensionSizes[5];

            return x * dimensionLengths[1] * dimensionLengths[2] * dimensionLengths[3] * dimensionLengths[4] * dimensionLengths[5] + y * dimensionLengths[2] * dimensionLengths[3] * dimensionLengths[4] * dimensionLengths[5] + z * dimensionLengths[3] * dimensionLengths[4] * dimensionLengths[5] + w * dimensionLengths[4] * dimensionLengths[5] + u * dimensionLengths[5] + v;
        }

        public long GetIndex(float x = 0f, float y = 0f, float z = 0f, float w = 0f, float u = 0f, float v = 0f)
        {
            x = XMath.Abs(x) % 1.0f; y = XMath.Abs(y) % 1.0f; z = XMath.Abs(z) % 1.0f;
            w = XMath.Abs(w) % 1.0f; u = XMath.Abs(u) % 1.0f; v = XMath.Abs(v) % 1.0f;

            return (long)(x * dimensionLengths[1] * dimensionLengths[2] * dimensionLengths[3] * dimensionLengths[4] * dimensionLengths[5] +
                   y * dimensionLengths[2] * dimensionLengths[3] * dimensionLengths[4] * dimensionLengths[5] +
                   z * dimensionLengths[3] * dimensionLengths[4] * dimensionLengths[5] +
                   w * dimensionLengths[4] * dimensionLengths[5] +
                   u * dimensionLengths[5] +
                   v);
        }
    }
}
