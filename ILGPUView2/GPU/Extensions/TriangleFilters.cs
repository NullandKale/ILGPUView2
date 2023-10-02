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


    public interface IClearFramebuffer
    {
        float GetDepthClearColor();
        RGBA32 GetColorClearColor();
    }


    public interface ITriangleImageFilterTiled : IClearFramebuffer
    {
        const int tileSize = 8;
        float GetNear();
        float GetFar();
        Mat4x4 GetCameraMat();

        // Vertex shader function
        TransformedTriangle VertShader(Triangle original, dMesh mesh, int width, int height);

        // Fragment shader function
        RGBA32 FragShader(float x, float y, TransformedTriangle triangle, float i);
    }

    public struct TransformedTriangle
    {
        public Vec3 wTerm;
        public Vec3 v0;
        public Vec3 v1;
        public Vec3 v2;

        public Vec3 origV0;
        public Vec3 origV1;
        public Vec3 origV2;

        public float det3D;

        // Bounding box corners
        public float minX;
        public float minY;
        public float maxX;
        public float maxY;

        public TransformedTriangle(Vec3 wTerm, Vec3 v0, Vec3 v1, Vec3 v2, Vec3 origV0, Vec3 origV1, Vec3 origV2, float det3D, float minX, float minY, float maxX, float maxY)
        {
            this.wTerm = wTerm;
            this.v0 = v0;
            this.v1 = v1;
            this.v2 = v2;
            this.origV0 = origV0;
            this.origV1 = origV1;
            this.origV2 = origV2;
            this.det3D = det3D;
            this.minX = minX;
            this.minY = minY;
            this.maxX = maxX;
            this.maxY = maxY;
        }
    }

    public static partial class Kernels
    {
        private static void DrawTile<TFunc>(int tick, int xMin, int yMin, int xMax, int yMax, FrameBuffer output, dMesh mesh, TFunc filter) where TFunc : unmanaged, ITriangleImageFilterTiled
        {
            float epsilon = ITriangleImageFilterTiled.tileSize * 200.0f;

            for (int i = 0; i < mesh.triangles.Length; i++)
            {
                // Skip if the triangle is completely outside the tile
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

                Vec3 origV0 = mesh.workingTriangles[i].origV0;
                Vec3 origV1 = mesh.workingTriangles[i].origV1;
                Vec3 origV2 = mesh.workingTriangles[i].origV2;

                // Skip triangles that are entirely behind the camera

                // this actually skips all the triangles in front of an plane about 1 unit in front of the camera
                //if (origV0.z < 0 || origV1.z < 0 || origV2.z < 0)
                //{
                //    continue;
                //}


                float w0 = mesh.workingTriangles[i].wTerm.x;
                float w1 = mesh.workingTriangles[i].wTerm.y;
                float w2 = mesh.workingTriangles[i].wTerm.z;

                float det3D = mesh.workingTriangles[i].det3D;

                // this seems to work correctly
                if (det3D < 0)
                {
                    continue;
                }

                // 2D determinants and inverse for screen-space barycentrics
                float vec_x1 = v1.x - v0.x;
                float vec_y1 = v1.y - v0.y;
                float vec_x2 = v2.x - v0.x;
                float vec_y2 = v2.y - v0.y;

                float invDet = 1.0f / (vec_x1 * vec_y2 - vec_x2 * vec_y1);

                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        float fx = (float)x / output.width * 2.0f - 1.0f;
                        float fy = (float)y / output.height * 2.0f - 1.0f;

                        float vec_px = fx - v0.x;
                        float vec_py = fy - v0.y;

                        // 2D Screen-space barycentrics
                        float alpha = (vec_px * vec_y2 - vec_x2 * vec_py) * invDet;
                        float beta = (vec_x1 * vec_py - vec_px * vec_y1) * invDet;
                        float gamma = 1.0f - alpha - beta;

                        // Interpolate 3D coordinates based on 2D barycentrics
                        Vec3 interpolated3D = alpha * origV0 + beta * origV1 + gamma * origV2;

                        Vec3 vecA = origV1 - origV0;
                        Vec3 vecB = origV2 - origV0;
                        Vec3 vecC = interpolated3D - origV0;

                        Vec3 crossBC = Vec3.cross(vecB, vecC);
                        Vec3 crossCA = Vec3.cross(vecC, vecA);
                        Vec3 crossAB = Vec3.cross(vecA, vecB);

                        float areaABC = Vec3.dot(vecA, Vec3.cross(vecB, vecC));
                        float alpha3D = Vec3.dot(vecB, crossBC) / areaABC;
                        float beta3D = Vec3.dot(vecC, crossCA) / areaABC;
                        float gamma3D = Vec3.dot(vecA, crossAB) / areaABC;
                        // END: Block to calculate 3D barycentric coordinates

                        bool useNewMethod = false;

                        if (useNewMethod)
                        {
                            if (alpha >= 0 && alpha <= 1 && beta >= 0 && beta <= 1 && gamma >= 0 && gamma <= 1)
                            {
                                float depthValue = alpha * v0.z + beta * v1.z + gamma * v2.z;
                                float interpolatedW = alpha3D * w0 + beta3D * w1 + gamma3D * w2;

                                float normalizedDepth = 1.0f - ((depthValue - filter.GetNear()) / (filter.GetFar() - filter.GetNear()));

                                RGBA32 debugColor;
                                if (interpolatedW > 0)
                                {
                                    debugColor = new RGBA32(0, 255, 0, 255); // Green for w > 0
                                }
                                else
                                {
                                    debugColor = new RGBA32(255, 0, 0, 255); // Red for w <= 0
                                }
                                output.SetColorAt(x, y, debugColor);
                                          // --------------------------------

                                if (interpolatedW > 0 && normalizedDepth < output.GetDepth(x, y))
                                {
                                    output.SetDepthPixel(x, y, normalizedDepth);
                                    RGBA32 color = filter.FragShader(x, y, mesh.workingTriangles[i], (float)i / (float)mesh.triangles.Length);
                                    output.SetColorAt(x, y, color);
                                }
                            }
                        }
                        else
                        {
                            // this works but the output is weird, it seems like it draws parts of triangles that are behind the camera so they rasterize weird
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
        }


        public static void ClearFramebufferKernel<TFunc>(Index1D index, FrameBuffer output, TFunc filter) where TFunc : unmanaged, IClearFramebuffer
        {
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

            DrawTile(tick, startX, startY, endX, endY, output, mesh, filter);
        }
    }

    public partial class Renderer
    {
        public void ExecuteTriangleFilterMany<TFunc>(GPUFrameBuffer output, GPUMesh mesh, TFunc filter = default) where TFunc : unmanaged, ITriangleImageFilterTiled
        {
            int numTiles = (output.width / ITriangleImageFilterTiled.tileSize) * (output.height / ITriangleImageFilterTiled.tileSize);

            var drawKernel = GetTriangleImageFilterManyKernel(filter);
            var clearKernel = GetClearFrameBufferKernel(filter);

            FrameBuffer framebuffer = output.toDevice(this);
            clearKernel(numTiles, framebuffer, filter);

            dMesh deviceMesh = mesh.toGPU(this);
            deviceMesh.ApplyCamera(filter.GetCameraMat());

            drawKernel(numTiles, numTiles, ticks, framebuffer, deviceMesh, filter);
        }

        public void ExecuteTriangleFilterMany<TFunc>(GPUFrameBuffer output, List<GPUMesh> meshes, TFunc filter = default) where TFunc : unmanaged, ITriangleImageFilterTiled
        {
            int numTiles = (output.width / ITriangleImageFilterTiled.tileSize) * (output.height / ITriangleImageFilterTiled.tileSize);

            var drawKernel = GetTriangleImageFilterManyKernel(filter);
            var clearKernel = GetClearFrameBufferKernel(filter);

            FrameBuffer framebuffer = output.toDevice(this);
            clearKernel(numTiles, framebuffer, filter);

            foreach ( GPUMesh mesh in meshes )
            {
                dMesh deviceMesh = mesh.toGPU(this);
                deviceMesh.ApplyCamera(filter.GetCameraMat());

                drawKernel(numTiles, numTiles, ticks, framebuffer, deviceMesh, filter);
            }
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

        private Action<Index1D, FrameBuffer, TFunc> GetClearFrameBufferKernel<TFunc>(TFunc filter = default) where TFunc : unmanaged, IClearFramebuffer
        {
            if (!kernels.ContainsKey(typeof(IClearFramebuffer)))
            {
                var kernel = device.LoadAutoGroupedStreamKernel<Index1D, FrameBuffer, TFunc>(ClearFramebufferKernel);
                kernels.Add(typeof(IClearFramebuffer), kernel);
            }

            return (Action<Index1D, FrameBuffer, TFunc>)kernels[typeof(IClearFramebuffer)];
        }
    }
}
