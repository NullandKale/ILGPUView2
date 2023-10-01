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
using Camera;
using System.Windows.Shapes;
using System.Windows;
using ILGPU.Algorithms;

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

    public struct TransformedTriangle
    {
        public Vec3 v0;
        public Vec3 v1;
        public Vec3 v2;

        // Bounding box corners
        public float minX;
        public float minY;
        public float maxX;
        public float maxY;

        public TransformedTriangle(Vec3 v0, Vec3 v1, Vec3 v2, float minX, float minY, float maxX, float maxY)
        {
            this.v0 = v0;
            this.v1 = v1;
            this.v2 = v2;
            this.minX = minX;
            this.minY = minY;
            this.maxX = maxX;
            this.maxY = maxY;
        }
    }


    public interface ITriangleImageFilterTiled
    {
        const int tileSize = 8;
        float GetDepthClearColor();
        RGBA32 GetColorClearColor();
        Mat4x4 GetCameraMat();
        void DrawTile(int tick, int xMin, int yMin, int xMax, int yMax, FrameBuffer output, dMesh mesh);
    }

    public static partial class Kernels
    {
        public static void TriangleImageFilterManyKernel<TFunc>(Index1D index, int threadCount, int tick, FrameBuffer output, dMesh mesh, TFunc filter) where TFunc : unmanaged, ITriangleImageFilterTiled
        {
            int numTriangles = mesh.triangles.IntLength;
            int trianglesPerThread = numTriangles / threadCount;
            int startIdx = index.X * trianglesPerThread;
            int endIdx = (index.X == threadCount - 1) ? numTriangles : (index.X + 1) * trianglesPerThread;

            // Pretransform triangles and store them in mesh.workingTriangles
            for (int i = startIdx; i < endIdx; ++i)
            {
                Triangle original = mesh.triangles[i];

                Vec4 v0 = mesh.matrix.MultiplyVector(new Vec4(original.v0.x, original.v0.y, original.v0.z, 1.0f));
                Vec4 v1 = mesh.matrix.MultiplyVector(new Vec4(original.v1.x, original.v1.y, original.v1.z, 1.0f));
                Vec4 v2 = mesh.matrix.MultiplyVector(new Vec4(original.v2.x, original.v2.y, original.v2.z, 1.0f));

                Triangle t = new Triangle(
                    new Vec3(v0.x / v0.w, v0.y / v0.w, v0.z / v0.w),
                    new Vec3(v1.x / v1.w, v1.y / v1.w, v1.z / v1.w),
                    new Vec3(v2.x / v2.w, v2.y / v2.w, v2.z / v2.w)
                );

                Vec3 pv0 = new Vec3((t.v0.x + 1.0f) * output.width / 2f, (t.v0.y + 1.0f) * output.height / 2f, t.v0.z);
                Vec3 pv1 = new Vec3((t.v1.x + 1.0f) * output.width / 2f, (t.v1.y + 1.0f) * output.height / 2f, t.v1.z);
                Vec3 pv2 = new Vec3((t.v2.x + 1.0f) * output.width / 2f, (t.v2.y + 1.0f) * output.height / 2f, t.v2.z);

                float minX = XMath.Min(pv0.x, XMath.Min(pv1.x, pv2.x));
                float minY = XMath.Min(pv0.y, XMath.Min(pv1.y, pv2.y));
                float maxX = XMath.Max(pv0.x, XMath.Max(pv1.x, pv2.x));
                float maxY = XMath.Max(pv0.y, XMath.Max(pv1.y, pv2.y));

                mesh.workingTriangles[i] = new TransformedTriangle(t.v0, t.v1, t.v2, minX, minY, maxX, maxY);
            }

            int startX = (index.X % (output.width / ITriangleImageFilterTiled.tileSize)) * ITriangleImageFilterTiled.tileSize;
            int startY = (index.X / (output.width / ITriangleImageFilterTiled.tileSize)) * ITriangleImageFilterTiled.tileSize;

            int endX = Math.Min(startX + ITriangleImageFilterTiled.tileSize, output.width);
            int endY = Math.Min(startY + ITriangleImageFilterTiled.tileSize, output.height);

            for (int y = 0; y < ITriangleImageFilterTiled.tileSize; ++y)
            {
                for (int x = 0; x < ITriangleImageFilterTiled.tileSize; ++x)
                {
                    output.SetDepthPixel(startX + x, startY + y, filter.GetDepthClearColor());
                    output.SetColorAt(startX + x, startY + y, filter.GetColorClearColor());
                }
            }

            filter.DrawTile(tick, startX, startY, endX, endY, output, mesh);
        }
    }

    public partial class Device
    {
        public void ExecuteTriangleFilterMany<TFunc>(GPUFrameBuffer output, GPUMesh mesh, TFunc filter = default) where TFunc : unmanaged, ITriangleImageFilterTiled
        {
            int numTiles = (output.width / ITriangleImageFilterTiled.tileSize) * (output.height / ITriangleImageFilterTiled.tileSize);
            var kernel = GetTriangleImageFilterManyKernel(filter);

            dMesh deviceMesh = mesh.toGPU(this);
            deviceMesh.ApplyCamera(filter.GetCameraMat());

            kernel(numTiles, numTiles, ticks, output.toDevice(this), deviceMesh, filter);
        }

        private Action<Index1D, int, int, FrameBuffer, dMesh, TFunc> GetTriangleImageFilterManyKernel<TFunc>(TFunc filter = default) where TFunc : unmanaged, ITriangleImageFilterTiled
        {
            if (!kernels.ContainsKey(filter.GetType()))
            {
                var kernel = device.LoadAutoGroupedStreamKernel<Index1D, int, int, FrameBuffer, dMesh, TFunc>(TriangleImageFilterManyKernel);
                kernels.Add(filter.GetType(), kernel);
            }

            return (Action<Index1D, int, int, FrameBuffer, dMesh, TFunc>)kernels[filter.GetType()];
        }
    }
}
