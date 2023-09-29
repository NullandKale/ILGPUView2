using GPU.RT;
using ILGPU.Runtime;
using ILGPU;
using ILGPUView2.GPU.DataStructures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static GPU.Kernels;

namespace GPU
{
    public struct Triangle
    {
        public Vec3 v0;
        public Vec3 v1;
        public Vec3 v2;

        public Triangle(Vec3 v0, Vec3 v1, Vec3 v2)
        {
            this.v0 = v0;
            this.v1 = v1;
            this.v2 = v2;
        }
    }

    public interface ITriangleImageFilter
    {
        RGBA32 Draw(int tick, float x, float y, dImage output, ArrayView1D<Triangle, Stride1D.Dense> triangles);
    }

    public interface ITriangleImageFilterMany
    {
        const int tileSize = 8;
        void DrawMany(int tick, int xMin, int yMin, int xMax, int yMax, dImage output, ArrayView2D<float, Stride2D.DenseY> localDepth, ArrayView1D<Triangle, Stride1D.Dense> triangles);
    }

    public static partial class Kernels
    {

        public static void TriangleImageFilterKernel<TFunc>(Index1D index, int tick, dImage output, ArrayView1D<Triangle, Stride1D.Dense> triangles, TFunc filter) where TFunc : unmanaged, ITriangleImageFilter
        {
            int x = index.X % output.width;
            int y = index.X / output.width;

            float u = (float)x / (float)output.width;
            float v = (float)y / (float)output.height;

            Vec2 uv = new Vec2(u, v);

            output.SetColorAt(x, y, filter.Draw(tick, uv.x, uv.y, output, triangles));
        }

        public static void TriangleImageFilterManyKernel<TFunc>(Index1D index, int tick, dImage output, ArrayView1D<Triangle, Stride1D.Dense> triangles, TFunc filter) where TFunc : unmanaged, ITriangleImageFilterMany
        {
            int startX = (index.X % (output.width / ITriangleImageFilterMany.tileSize)) * ITriangleImageFilterMany.tileSize;
            int startY = (index.X / (output.width / ITriangleImageFilterMany.tileSize)) * ITriangleImageFilterMany.tileSize;

            int endX = Math.Min(startX + ITriangleImageFilterMany.tileSize, output.width);
            int endY = Math.Min(startY + ITriangleImageFilterMany.tileSize, output.height);

            ArrayView2D<float, Stride2D.DenseY> depthBuffer = LocalMemory.Allocate2D<float, Stride2D.DenseY>(new Index2D(ITriangleImageFilterMany.tileSize, ITriangleImageFilterMany.tileSize), new Stride2D.DenseY());
            ArrayView1D<Triangle, Stride1D.Dense> tileTriangles = LocalMemory.Allocate1D<Triangle>(64);

            for (int y = 0; y < ITriangleImageFilterMany.tileSize; ++y)
            {
                for (int x = 0; x < ITriangleImageFilterMany.tileSize; ++x)
                {
                    depthBuffer[x, y] = float.MaxValue;

                    RGBA32 clearColor = new RGBA32(0, 0, 0);

                    output.SetColorAt(startX + x, startY + y, clearColor);
                }
            }

            filter.DrawMany(tick, startX, startY, endX, endY, output, depthBuffer, triangles);
        }
    }

    public partial class Device
    {
        public void ExecuteTriangleFilter<TFunc>(GPUImage output, MemoryBuffer1D<Triangle, Stride1D.Dense> triangles, TFunc filter = default) where TFunc : unmanaged, ITriangleImageFilter
        {
            var kernel = GetTriangleImageFilterKernel(filter);
            kernel(output.width * output.height, ticks, output.toDevice(this), triangles, filter);
        }

        private Action<Index1D, int, dImage, ArrayView1D<Triangle, Stride1D.Dense>, TFunc> GetTriangleImageFilterKernel<TFunc>(TFunc filter = default) where TFunc : unmanaged, ITriangleImageFilter
        {
            if (!kernels.ContainsKey(filter.GetType()))
            {
                var kernel = device.LoadAutoGroupedStreamKernel<Index1D, int, dImage, ArrayView1D<Triangle, Stride1D.Dense>, TFunc>(TriangleImageFilterKernel);
                kernels.Add(filter.GetType(), kernel);
            }

            return (Action<Index1D, int, dImage, ArrayView1D<Triangle, Stride1D.Dense>, TFunc>)kernels[filter.GetType()];
        }

        public void ExecuteTriangleFilterMany<TFunc>(GPUImage output, MemoryBuffer1D<Triangle, Stride1D.Dense> triangles, TFunc filter = default) where TFunc : unmanaged, ITriangleImageFilterMany
        {
            int numTiles = (output.width / ITriangleImageFilterMany.tileSize) * (output.height / ITriangleImageFilterMany.tileSize);
            var kernel = GetTriangleImageFilterManyKernel(filter);
            kernel(numTiles, ticks, output.toDevice(this), triangles, filter);
        }

        private Action<Index1D, int, dImage, ArrayView1D<Triangle, Stride1D.Dense>, TFunc> GetTriangleImageFilterManyKernel<TFunc>(TFunc filter = default) where TFunc : unmanaged, ITriangleImageFilterMany
        {
            if (!kernels.ContainsKey(filter.GetType()))
            {
                var kernel = device.LoadAutoGroupedStreamKernel<Index1D, int, dImage, ArrayView1D<Triangle, Stride1D.Dense>, TFunc>(TriangleImageFilterManyKernel);
                kernels.Add(filter.GetType(), kernel);
            }

            return (Action<Index1D, int, dImage, ArrayView1D<Triangle, Stride1D.Dense>, TFunc>)kernels[filter.GetType()];
        }
    }
}
