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
using System.Windows.Media.Media3D;

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
        public Vec3 v0, v1, v2;
        //public Vec3 origV0, origV1, origV2;
        //public float det3D;
        public float minX, minY, maxX, maxY;

        // Bitwise flags to store state. 0 for OK, otherwise rejected for various reasons.
        public int stateFlags;

        // v0, v1, v2 are all in camera space
        // width and height are the framebuffer size
        public TransformedTriangle(Vec4 v0, Vec4 v1, Vec4 v2, int width, int height)
        {
            Vec3 origV0 = new Vec3(v0.x, v0.y, v0.z);
            Vec3 origV1 = new Vec3(v1.x, v1.y, v1.z);
            Vec3 origV2 = new Vec3(v2.x, v2.y, v2.z);

            this.v0 = origV0 / v0.w;
            this.v1 = origV1 / v1.w;
            this.v2 = origV2 / v2.w;

            float det3D = Vec3.cross(this.v1 - this.v0, this.v2 - this.v0).length();

            wTerm = new Vec3(v0.w, v1.w, v2.w);

            Vec3 pv0 = new Vec3((this.v0.x + 1.0f) * width / 2f, (this.v0.y + 1.0f) * height / 2f, this.v0.z);
            Vec3 pv1 = new Vec3((this.v1.x + 1.0f) * width / 2f, (this.v1.y + 1.0f) * height / 2f, this.v1.z);
            Vec3 pv2 = new Vec3((this.v2.x + 1.0f) * width / 2f, (this.v2.y + 1.0f) * height / 2f, this.v2.z);

            minX = XMath.Min(pv0.x, XMath.Min(pv1.x, pv2.x));
            minY = XMath.Min(pv0.y, XMath.Min(pv1.y, pv2.y));
            maxX = XMath.Max(pv0.x, XMath.Max(pv1.x, pv2.x));
            maxY = XMath.Max(pv0.y, XMath.Max(pv1.y, pv2.y));

            // Initialize stateFlags to 0 (OK)
            stateFlags = 0;

            // If the triangle is behind the camera, set the 1st bit to 1.
            if (origV0.z > 1 || origV1.z > 1 || origV2.z > 1)
            {
                stateFlags |= 1;
            }

            // If the triangle is backfacing, set the 2nd bit to 1.
            if (det3D < 0)
            {
                stateFlags |= 2;
            }
        }
    }


    public static partial class Kernels
    {
        private static void DrawTile<TFunc>(int tick, int xMin, int yMin, int xMax, int yMax, FrameBuffer output, dMesh mesh, TFunc filter) where TFunc : unmanaged, ITriangleImageFilterTiled
        {
            float near = filter.GetNear();
            float far = filter.GetFar();
            float invFarNearDiff = 1.0f / (far - near);
            float widthFactor = 2.0f / output.width;
            float heightFactor = 2.0f / output.height;

            for (int i = 0; i < mesh.triangles.Length; i++)
            {
                TransformedTriangle workingTriangle = mesh.workingTriangles[i];

                // Skip if any state flag is set.
                if (workingTriangle.stateFlags != 0)
                {
                    continue;
                }

                if (workingTriangle.maxX < xMin || workingTriangle.minX > xMax || workingTriangle.maxY < yMin || workingTriangle.minY > yMax)
                {
                    continue;
                }

                int minX = XMath.Max((int)XMath.Floor(workingTriangle.minX), xMin);
                int minY = XMath.Max((int)XMath.Floor(workingTriangle.minY), yMin);
                int maxX = XMath.Min((int)XMath.Ceiling(workingTriangle.maxX), xMax);
                int maxY = XMath.Min((int)XMath.Ceiling(workingTriangle.maxY), yMax);

                Vec3 v0 = workingTriangle.v0;
                Vec3 v1 = workingTriangle.v1;
                Vec3 v2 = workingTriangle.v2;

                float vec_x1 = v1.x - v0.x;
                float vec_y1 = v1.y - v0.y;
                float vec_x2 = v2.x - v0.x;
                float vec_y2 = v2.y - v0.y;

                float invDet = 1.0f / (vec_x1 * vec_y2 - vec_x2 * vec_y1);

                for (int y = minY; y <= maxY; y++)
                {
                    float fy = y * heightFactor - 1.0f;

                    for (int x = minX; x <= maxX; x++)
                    {
                        float fx = x * widthFactor - 1.0f;
                        float vec_px = fx - v0.x;
                        float vec_py = fy - v0.y;

                        float alpha = (vec_px * vec_y2 - vec_x2 * vec_py) * invDet;
                        float beta = (vec_x1 * vec_py - vec_px * vec_y1) * invDet;
                        float gamma = 1.0f - alpha - beta;

                        if (alpha >= 0 && alpha <= 1 && beta >= 0 && beta <= 1 && gamma >= 0 && gamma <= 1)
                        {
                            float depthValue = alpha * v0.z + beta * v1.z + gamma * v2.z;
                            float interpolatedW = alpha * workingTriangle.wTerm.x + beta * workingTriangle.wTerm.y + gamma * workingTriangle.wTerm.z;

                            float normalizedDepth = 1.0f - ((depthValue - near) * invFarNearDiff);

                            if (interpolatedW > 0 && normalizedDepth < output.GetDepth(x, y))
                            {
                                output.SetDepthPixel(x, y, normalizedDepth);
                                RGBA32 color = filter.FragShader(x, y, workingTriangle, (float)i / (float)mesh.triangles.Length);
                                output.SetColorAt(x, y, color);
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
