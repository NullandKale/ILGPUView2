﻿using GPU.RT;
using ILGPU.Runtime;
using ILGPU;
using ILGPUView2.GPU.DataStructures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static GPU.Kernels;
using Camera;

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

    public interface ITriangleImageFilterTiled
    {
        const int tileSize = 8;

        float GetDepthClearColor();
        RGBA32 GetColorClearColor();
        void DrawTile(int tick, int xMin, int yMin, int xMax, int yMax, FrameBuffer output, ArrayView1D<Triangle, Stride1D.Dense> triangles);
    }

    public static partial class Kernels
    {

        public static void TriangleImageFilterManyKernel<TFunc>(Index1D index, int tick, FrameBuffer output, ArrayView1D<Triangle, Stride1D.Dense> triangles, TFunc filter) where TFunc : unmanaged, ITriangleImageFilterTiled
        {
            int startX = (index.X % (output.width / ITriangleImageFilterTiled.tileSize)) * ITriangleImageFilterTiled.tileSize;
            int startY = (index.X / (output.width / ITriangleImageFilterTiled.tileSize)) * ITriangleImageFilterTiled.tileSize;

            int endX = Math.Min(startX + ITriangleImageFilterTiled.tileSize, output.width);
            int endY = Math.Min(startY + ITriangleImageFilterTiled.tileSize, output.height);

            //ArrayView1D<Triangle, Stride1D.Dense> tileTriangles = LocalMemory.Allocate1D<Triangle>(64);

            for (int y = 0; y < ITriangleImageFilterTiled.tileSize; ++y)
            {
                for (int x = 0; x < ITriangleImageFilterTiled.tileSize; ++x)
                {
                    output.SetDepthPixel(startX + x, startY + y, filter.GetDepthClearColor());
                    output.SetColorAt(startX + x, startY + y, filter.GetColorClearColor());
                }
            }

            filter.DrawTile(tick, startX, startY, endX, endY, output, triangles);
        }
    }

    public partial class Device
    {
        public void ExecuteTriangleFilterMany<TFunc>(GPUFrameBuffer output, MemoryBuffer1D<Triangle, Stride1D.Dense> triangles, TFunc filter = default) where TFunc : unmanaged, ITriangleImageFilterTiled
        {
            int numTiles = (output.width / ITriangleImageFilterTiled.tileSize) * (output.height / ITriangleImageFilterTiled.tileSize);
            var kernel = GetTriangleImageFilterManyKernel(filter);
            kernel(numTiles, ticks, output.toDevice(this), triangles, filter);
        }

        private Action<Index1D, int, FrameBuffer, ArrayView1D<Triangle, Stride1D.Dense>, TFunc> GetTriangleImageFilterManyKernel<TFunc>(TFunc filter = default) where TFunc : unmanaged, ITriangleImageFilterTiled
        {
            if (!kernels.ContainsKey(filter.GetType()))
            {
                var kernel = device.LoadAutoGroupedStreamKernel<Index1D, int, FrameBuffer, ArrayView1D<Triangle, Stride1D.Dense>, TFunc>(TriangleImageFilterManyKernel);
                kernels.Add(filter.GetType(), kernel);
            }

            return (Action<Index1D, int, FrameBuffer, ArrayView1D<Triangle, Stride1D.Dense>, TFunc>)kernels[filter.GetType()];
        }
    }
}
