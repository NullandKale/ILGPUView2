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
        float GetNear();
        float GetFar();
        float GetDepthClearColor();
        RGBA32 GetColorClearColor();
        Mat4x4 GetCameraMat();

        // Vertex shader function
        TransformedTriangle VertShader(Triangle original, dMesh mesh, int width, int height);

        // Fragment shader function
        RGBA32 FragShader(float x, float y, TransformedTriangle triangle, float i);
    }

    public static partial class Kernels
    {
        private static void DrawTile<TFunc>(int tick, int xMin, int yMin, int xMax, int yMax, FrameBuffer output, dMesh mesh, TFunc filter) where TFunc : unmanaged, ITriangleImageFilterTiled
        {
            // needs to be really high for skinny triangles
            float epsilon = ITriangleImageFilterTiled.tileSize * 200.0f;

            for (int i = 0; i < mesh.triangles.Length; i++)
            {
                // Early exit if the triangle is completely outside the tile
                if (mesh.workingTriangles[i].maxX < xMin - epsilon || mesh.workingTriangles[i].minX > xMax + epsilon || mesh.workingTriangles[i].maxY < yMin - epsilon || mesh.workingTriangles[i].minY > yMax + epsilon)
                {
                    continue;
                }

                int minX = XMath.Max((int)XMath.Floor(mesh.workingTriangles[i].minX), xMin);
                int minY = XMath.Max((int)XMath.Floor(mesh.workingTriangles[i].minY), yMin);
                int maxX = XMath.Min((int)XMath.Ceiling(mesh.workingTriangles[i].maxX), xMax);
                int maxY = XMath.Min((int)XMath.Ceiling(mesh.workingTriangles[i].maxY), yMax);

                Vec3 v0 = mesh.workingTriangles[i].v0;
                Vec3 v1 = mesh.workingTriangles[i].v1;
                Vec3 v2 = mesh.workingTriangles[i].v2;

                float vec_x1 = v1.x - v0.x;
                float vec_y1 = v1.y - v0.y;
                float vec_x2 = v2.x - v0.x;
                float vec_y2 = v2.y - v0.y;

                float det = vec_x1 * vec_y2 - vec_x2 * vec_y1;

                if (det > 0)
                {
                    continue;
                }

                float invDet = 1.0f / det;

                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        float fx = (float)x / output.width * 2.0f - 1.0f;
                        float fy = (float)y / output.height * 2.0f - 1.0f;

                        float vec_px = fx - v0.x;
                        float vec_py = fy - v0.y;

                        float alpha = (vec_px * vec_y2 - vec_x2 * vec_py) * invDet;
                        float beta = (vec_x1 * vec_py - vec_px * vec_y1) * invDet;
                        float gamma = 1.0f - alpha - beta;

                        bool isInTriangle = (alpha >= 0 && alpha <= 1) &&
                                            (beta >= 0 && beta <= 1) &&
                                            (gamma >= 0 && gamma <= 1);

                        if (isInTriangle)
                        {
                            float depthValue = (alpha * v0.z + beta * v1.z + gamma * v2.z);
                            float normalizedDepth = 1.0f - ((depthValue - filter.GetNear()) / (filter.GetFar() - filter.GetNear()));

                            if (normalizedDepth < output.GetDepth(x, y))
                            {
                                output.SetDepthPixel(x, y, normalizedDepth);

                                RGBA32 color = filter.FragShader(x, y, mesh.workingTriangles[i], (float)i / (float)mesh.triangles.Length);
                                //RGBA32 color = new RGBA32(normalizedDepth, normalizedDepth, normalizedDepth);

                                output.SetColorAt(x, y, color);
                            }
                        }
                    }
                }
            }
        }

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
                TransformedTriangle transformed = filter.VertShader(original, mesh, output.width, output.height);
                mesh.workingTriangles[i] = transformed;
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

            DrawTile(tick, startX, startY, endX, endY, output, mesh, filter);
        }
    }

    public partial class Renderer
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
