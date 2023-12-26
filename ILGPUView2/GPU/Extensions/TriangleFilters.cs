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
using System.Windows.Shapes;
using System.Windows;
using ILGPU.Algorithms;
using System.Windows.Media.Media3D;
using System.Reflection.Metadata.Ecma335;
using System.Diagnostics;
using ILGPUView2.GPU.Extensions;

namespace GPU
{
    // this is the debug shader
    public unsafe struct DrawTrianglesTiled : ITriangleImageFilterTiled
    {
        public int tick;
        public float aspectRatio;
        public float near;
        public float far;

        public Mat4x4 cameraMatrix;

        public DrawTrianglesTiled(Vec3 cameraPos, Vec3 up, Vec3 lookAt, int sizeX, int sizeY, float hFovDegrees, float near, float far, int tick)
        {
            this.tick = tick;
            this.near = near;
            this.far = far;
            this.aspectRatio = (float)sizeX / (float)sizeY;

            cameraMatrix = Mat4x4.CreateCameraMatrix(cameraPos, up, lookAt, sizeX, sizeY, hFovDegrees, near, far);
        }

        public Mat4x4 GetCameraMat()
        {
            return cameraMatrix;
        }

        public RGBA32 GetColorClearColor()
        {
            // float float float 0 - 1 color constructor
            return new RGBA32(0, 0, 0);
        }

        public float GetDepthClearColor()
        {
            return float.MinValue;
        }

        public float GetFar()
        {
            return far;
        }

        public float GetNear()
        {
            return near;
        }

        public RGBA32 FragShader(float x, float y, TransformedTriangle triangle, float i, dMegaTexture textures)
        {
            return textures.GetColorAt(0, x, y);
        }

        public TransformedTriangle VertShader(Triangle original, Mat4x4 matrix, int width, int height)
        {
            Vec4 v0 = matrix.MultiplyVector(new Vec4(original.v0.x, original.v0.y, original.v0.z, 1.0f));
            Vec4 v1 = matrix.MultiplyVector(new Vec4(original.v1.x, original.v1.y, original.v1.z, 1.0f));
            Vec4 v2 = matrix.MultiplyVector(new Vec4(original.v2.x, original.v2.y, original.v2.z, 1.0f));

            return new TransformedTriangle(v0, v1, v2, original.uv0, original.uv1, original.uv2, width, height);
        }
    }

    // this is a single triangle

    public struct Triangle
    {
        public Vec3 v0;
        public Vec3 v1;
        public Vec3 v2;

        public Vec2 uv0;
        public Vec2 uv1;
        public Vec2 uv2;

        public Triangle(Vec3 v0, Vec3 v1, Vec3 v2)
        {
            this.v0 = v0;
            this.v1 = v1;
            this.v2 = v2;
        }

        public Triangle(Vec3 v0, Vec3 v1, Vec3 v2, Vec2 uv0, Vec2 uv1, Vec2 uv2)
        {
            this.v0 = v0;
            this.v1 = v1;
            this.v2 = v2;
            this.uv0 = uv0;
            this.uv1 = uv1;
            this.uv2 = uv2;
        }
    }

    // defines a clear color target

    public interface IClearFramebuffer
    {
        float GetDepthClearColor();
        RGBA32 GetColorClearColor();
    }

    public interface IVertShader
    {
        // Vertex shader function
        TransformedTriangle VertShader(Triangle original, Mat4x4 matrix, int width, int height);
    }

    // defines a shader
    public interface ITriangleImageFilterTiled : IClearFramebuffer, IVertShader
    {
        float GetNear();
        float GetFar();
        Mat4x4 GetCameraMat();

        // Fragment shader function
        RGBA32 FragShader(float x, float y, TransformedTriangle triangle, float i, dMegaTexture textures);
    }

    // this is the triangle output format, kinda like ther vertex format
    public struct TransformedTriangle
    {
        public Vec3 wTerm;
        public Vec3 v0, v1, v2;
        public Vec2 uv0, uv1, uv2;

        // this needs to be in pixel space 0 - width, 0 - height
        public float minX, minY, maxX, maxY;

        // Bitwise flags to store state. 0 for OK, otherwise rejected for various reasons.
        public int stateFlags;
        public int meshID;

        // v0, v1, v2 are all in camera space
        // width and height are the framebuffer size
        public TransformedTriangle(Vec4 v0, Vec4 v1, Vec4 v2, Vec2 uv0, Vec2 uv1, Vec2 uv2, int width, int height)
        {
            Vec3 origV0 = new Vec3(v0.x, v0.y, v0.z);
            Vec3 origV1 = new Vec3(v1.x, v1.y, v1.z);
            Vec3 origV2 = new Vec3(v2.x, v2.y, v2.z);

            // Assign UV coordinates
            this.uv0 = uv0;
            this.uv1 = uv1;
            this.uv2 = uv2;

            this.v0 = origV0 / v0.w;
            this.v1 = origV1 / v1.w;
            this.v2 = origV2 / v2.w;

            float det3D = Vec3.cross(this.v1 - this.v0, this.v2 - this.v0).length();

            wTerm = new Vec3(v0.w, v1.w, v2.w);

            Vec3 pv0 = new Vec3((this.v0.x + 1.0f) * width * 0.5f, (this.v0.y + 1.0f) * height * 0.5f, this.v0.z);
            Vec3 pv1 = new Vec3((this.v1.x + 1.0f) * width * 0.5f, (this.v1.y + 1.0f) * height * 0.5f, this.v1.z);
            Vec3 pv2 = new Vec3((this.v2.x + 1.0f) * width * 0.5f, (this.v2.y + 1.0f) * height * 0.5f, this.v2.z);

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
                //stateFlags |= 2;
            }
        }

        public float GetInterpolatedDepth(float alpha, float beta, float gamma)
        {
            // Using the barycentric coordinates, interpolate the depth
            float depth = alpha * v0.z + beta * v1.z + gamma * v2.z;
            return depth;
        }

    }


    public static partial class Kernels
    {
        // separate clear kernel for performance
        public static void ClearFramebufferKernel<TFunc>(Index1D index, FrameBuffer output, TFunc filter) where TFunc : unmanaged, IClearFramebuffer
        {
            int startX = (index.X % (output.width / RasterisationSettings.tileSize)) * RasterisationSettings.tileSize;
            int startY = (index.X / (output.width / RasterisationSettings.tileSize)) * RasterisationSettings.tileSize;

            int endX = Math.Min(startX + RasterisationSettings.tileSize, output.width);
            int endY = Math.Min(startY + RasterisationSettings.tileSize, output.height);

            for (int y = 0; y < RasterisationSettings.tileSize; ++y)
            {
                for (int x = 0; x < RasterisationSettings.tileSize; ++x)
                {
                    output.SetDepthPixel(startX + x, startY + y, filter.GetDepthClearColor());
                    output.SetColorAt(startX + x, startY + y, filter.GetColorClearColor());
                }
            }
        }

        public static void TriangleImageFilterManyTileCacheKernel<TFunc>(int length, FrameBuffer output, dMeshBatch meshes, dMegaTexture textures, ArrayView1D<TileTriangleRecord, Stride1D.Dense> perTileTriangleArray, ArrayView1D<int, Stride1D.Dense> perTileTriangleCount, TFunc filter) where TFunc : unmanaged, ITriangleImageFilterTiled
        {
            var index = Group.DimX * Grid.IdxX + Group.IdxX;

            if (index >= length)
            {
                return;
            }

            int count = XMath.Min(perTileTriangleCount[index], RasterisationSettings.maxTrianglesPerTile);

            if (count <= 0)
            {
                return;
            }

            int startX = (index % (output.width / RasterisationSettings.tileSize)) * RasterisationSettings.tileSize;
            int startY = (index / (output.width / RasterisationSettings.tileSize)) * RasterisationSettings.tileSize;

            int endX = Math.Min(startX + RasterisationSettings.tileSize, output.width);
            int endY = Math.Min(startY + RasterisationSettings.tileSize, output.height);

            if (RasterisationSettings.debugCountColor)
            {
                for (int y = startY; y <= endY; y++)
                {
                    for (int x = startX; x <= endX; x++)
                    {
                        output.SetColorAt(x, y, new RGBA32(count / (float)RasterisationSettings.maxTrianglesPerTile));
                    }
                }

                return;
            }

            float near = filter.GetNear();
            float far = filter.GetFar();
            float invFarNearDiff = 1.0f / (far - near);
            float widthFactor = 2.0f / output.width;
            float heightFactor = 2.0f / output.height;

            int linearIndex = index * RasterisationSettings.maxTrianglesPerTile;

            for (int i = 0; i < count; i++)
            {
                Group.Barrier();

                TileTriangleRecord triangleRecord = perTileTriangleArray[linearIndex + i];
                TransformedTriangle workingTriangle = meshes.GetWorkingTriangle(triangleRecord.meshID, triangleRecord.triangleIndex);

                // maybe this should be doubles?
                int minX = XMath.Max((int)XMath.Floor(workingTriangle.minX), startX);
                int minY = XMath.Max((int)XMath.Floor(workingTriangle.minY), startY);
                int maxX = XMath.Min((int)XMath.Ceiling(workingTriangle.maxX), endX);
                int maxY = XMath.Min((int)XMath.Ceiling(workingTriangle.maxY), endY);

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
                    float fy = y / (float)output.height * 2.0f - 1.0f;

                    for (int x = minX; x <= maxX; x++)
                    {
                        float fx = x / (float)output.width * 2.0f - 1.0f;

                        // Calculate barycentric coordinates for the pixel
                        float vec_px = fx - v0.x;
                        float vec_py = fy - v0.y;

                        float alpha = (vec_px * vec_y2 - vec_x2 * vec_py) * invDet;
                        float beta = (vec_x1 * vec_py - vec_px * vec_y1) * invDet;
                        float gamma = 1.0f - alpha - beta;

                        if (alpha >= 0 && alpha <= 1 && beta >= 0 && beta <= 1 && gamma >= 0 && gamma <= 1)
                        {
                            // Interpolate the inverse of the W value (for perspective correction)
                            float invW = 1.0f / ((alpha * workingTriangle.wTerm.x) + (beta * workingTriangle.wTerm.y) + (gamma * workingTriangle.wTerm.z));

                            if (invW > 0)
                            {
                                // Interpolate depth
                                float depthValue = workingTriangle.GetInterpolatedDepth(alpha, beta, gamma);

                                // Check depth
                                float currentDepth = output.GetDepth(x, y);

                                if (depthValue > currentDepth)
                                {
                                    output.SetDepthPixel(x, y, depthValue);

                                    // Perspective correct interpolation of UV coordinates
                                    // we know the imput uvs are perfect because they match
                                    Vec2 uv = (alpha * (workingTriangle.uv0 / workingTriangle.wTerm.x)
                                             + beta * (workingTriangle.uv1 / workingTriangle.wTerm.y)
                                             + gamma * (workingTriangle.uv2 / workingTriangle.wTerm.z))
                                             * invW;

                                    RGBA32 color = filter.FragShader(uv.x, uv.y, workingTriangle, (depthValue + 1.0f) * 0.5f, textures);
                                    //RGBA32 color = filter.FragShader(uv.x, uv.y, workingTriangle, alpha, textures);
                                    //RGBA32 color = filter.FragShader(uv.x, uv.y, workingTriangle, beta, textures);
                                    //RGBA32 color = filter.FragShader(uv.x, uv.y, workingTriangle, gamma, textures);
                                    output.SetColorAt(x, y, color);
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    public partial class Renderer
    {
        private Stopwatch clearStopwatch = new Stopwatch();
        private Stopwatch transformStopwatch = new Stopwatch();
        private Stopwatch tileCacheFillStopwatch = new Stopwatch();
        private Stopwatch drawStopwatch = new Stopwatch();
        
        private double totalClearTime = 0;
        private double totalTransformTime = 0;
        private double totalFillTileCacheTime = 0;
        private double totalDrawTime = 0;
        private int sampleCount = 0;

        private TileCache tileCache;
        public static bool timeEachStep = false;

        // this function draws multiple meshes to the output with the shader ITriangleImageFilterTiled on the gpu
        public void ExecuteTriangleFilterMany<TFunc>(GPUFrameBuffer output, GPUMeshBatch meshes, GPUMegaTexture textures, TFunc filter = default) where TFunc : unmanaged, ITriangleImageFilterTiled
        {
            int widthInTiles = output.width / RasterisationSettings.tileSize;
            int heightInTiles = output.height / RasterisationSettings.tileSize;
            int numTiles = widthInTiles * heightInTiles;

            if (tileCache == null)
            {
                tileCache = new TileCache(this, widthInTiles, heightInTiles);
            }
            else
            {
                if (tileCache.heightInTiles != heightInTiles || tileCache.widthInTiles != widthInTiles)
                {
                    tileCache.Dispose();
                    tileCache = new TileCache(this, widthInTiles, heightInTiles);
                }
            }

            var transformKernel = GetTransformTrianglesKernel(filter);
            var fillTileCacheKernel = GetFillTileCacheTrianglesKernel();
            var drawKernel = GetTriangleImageFilterManyTileCacheKernel(filter);
            var clearKernel = GetClearFrameBufferKernel(filter);

            FrameBuffer framebuffer = output.toDevice(this);

            (ArrayView1D<TileTriangleRecord, Stride1D.Dense> tileTriangles, ArrayView1D<int, Stride1D.Dense> tileTriangleCounts) = tileCache.GetCache();

            meshes.ApplyCamera(filter.GetCameraMat());

            // this is where the copy happens
            dMeshBatch deviceMeshes = meshes.toGPU(this);
            dMegaTexture deviceTextures = textures.ToGPU(this);

            clearStopwatch.Restart();
            clearKernel(numTiles, framebuffer, filter);
            if(timeEachStep)
            {
                device.Synchronize();
            }
            clearStopwatch.Stop();

            transformStopwatch.Restart();

            int totalTriangles = deviceMeshes.triangles.IntLength;
            int transformGroupDim = Math.Min(RasterisationSettings.transformGroupSize, device.MaxNumThreadsPerGroup);
            int transforGridDim = (totalTriangles + transformGroupDim - 1) / transformGroupDim;
            KernelConfig kernelConfig = new KernelConfig(transforGridDim, transformGroupDim);

            transformKernel(kernelConfig, totalTriangles, deviceMeshes, filter, output.width, output.height, tileTriangles, tileTriangleCounts);
            if (timeEachStep)
            {
                device.Synchronize();
            }
            transformStopwatch.Stop();

            tileCacheFillStopwatch.Restart();

            int fillGroupDim = Math.Min(RasterisationSettings.tileFillGroupSize, device.MaxNumThreadsPerGroup);
            int fillGridDim = (totalTriangles + fillGroupDim - 1) / fillGroupDim;
            KernelConfig fillKernelConfig = new KernelConfig(fillGridDim, fillGroupDim);

            fillTileCacheKernel(fillKernelConfig, totalTriangles, deviceMeshes, output.width, output.height, tileTriangles, tileTriangleCounts);
            if (timeEachStep)
            {
                device.Synchronize();
            }
            tileCacheFillStopwatch.Stop();

            drawStopwatch.Restart();

            int drawGroupDim = Math.Min(RasterisationSettings.drawFillGroupSize, device.MaxNumThreadsPerGroup);
            int drawGridDim = (numTiles + drawGroupDim - 1) / drawGroupDim;
            KernelConfig drawKernelConfig = new KernelConfig(drawGridDim, drawGroupDim);

            drawKernel(drawKernelConfig, numTiles, framebuffer, deviceMeshes, deviceTextures, tileTriangles, tileTriangleCounts, filter);
            if (timeEachStep)
            {
                device.Synchronize();
            }
            drawStopwatch.Stop();

            UpdateKernelTimings(clearStopwatch.Elapsed.TotalMilliseconds, transformStopwatch.Elapsed.TotalMilliseconds, tileCacheFillStopwatch.Elapsed.TotalMilliseconds, drawStopwatch.Elapsed.TotalMilliseconds);
        }

        private void UpdateKernelTimings(double clearTime, double transformTime, double fillTileCacheTime, double drawTime)
        {
            if(timeEachStep)
            {
                totalClearTime += clearTime;
                totalTransformTime += transformTime;
                totalFillTileCacheTime += fillTileCacheTime;
                totalDrawTime += drawTime;
                sampleCount++;
            }
        }

        public (double averageClearTime, double averageTransformTime, double averageFillTileCacheTime, double averageDrawTime, double averageTotalTime, int sampleCount) CalculateRasterizationKernelTimings()
        {
            double averageClearTime = totalClearTime / sampleCount;
            double averageTransformTime = totalTransformTime / sampleCount;
            double averageFillTileCacheTime = totalFillTileCacheTime / sampleCount;
            double averageDrawTime = totalDrawTime / sampleCount;

            double averageTotalTime = (averageClearTime + averageTransformTime + averageFillTileCacheTime + averageDrawTime);

            return (averageClearTime, averageTransformTime, averageFillTileCacheTime, averageDrawTime, averageTotalTime, sampleCount);
        }

        // this caches the gpu kernel for the vert shader kernel for each shader
        private Action<KernelConfig, int, FrameBuffer, dMeshBatch, dMegaTexture, ArrayView1D<TileTriangleRecord, Stride1D.Dense>, ArrayView1D<int, Stride1D.Dense>, TFunc> GetTriangleImageFilterManyTileCacheKernel<TFunc>(TFunc filter = default) where TFunc : unmanaged, ITriangleImageFilterTiled
        {
            if (!kernels.ContainsKey(filter.GetType()))
            {
                var kernel = device.LoadStreamKernel<int, FrameBuffer, dMeshBatch, dMegaTexture, ArrayView1D<TileTriangleRecord, Stride1D.Dense>, ArrayView1D<int, Stride1D.Dense>, TFunc>(TriangleImageFilterManyTileCacheKernel);
                kernels.Add(filter.GetType(), kernel);
            }

            return (Action<KernelConfig, int, FrameBuffer, dMeshBatch, dMegaTexture, ArrayView1D<TileTriangleRecord, Stride1D.Dense>, ArrayView1D<int, Stride1D.Dense>, TFunc>)kernels[filter.GetType()];
        }

        private Action<KernelConfig, int, dMeshBatch, TFunc, int, int, ArrayView1D<TileTriangleRecord, Stride1D.Dense>, ArrayView1D<int, Stride1D.Dense>> GetTransformTrianglesKernel<TFunc>(TFunc filter = default) where TFunc : unmanaged, IVertShader
        {
            // todo make this not use TileCache because it only works for one filter
            if (!kernels.ContainsKey(typeof(TileCache)))
            {
                var kernel = device.LoadStreamKernel<int, dMeshBatch, TFunc, int, int, ArrayView1D<TileTriangleRecord, Stride1D.Dense>, ArrayView1D<int, Stride1D.Dense>>(TileCache.TransformTrianglesKernel);
                kernels.Add(typeof(TileCache), kernel);
            }

            return (Action<KernelConfig, int, dMeshBatch, TFunc, int, int, ArrayView1D<TileTriangleRecord, Stride1D.Dense>, ArrayView1D<int, Stride1D.Dense>>)kernels[typeof(TileCache)];
        }

        Action<KernelConfig, int, dMeshBatch, int, int, ArrayView1D<TileTriangleRecord, Stride1D.Dense>, ArrayView1D<int, Stride1D.Dense>> fillTileCacheTrianglesKernel = null;
        private Action<KernelConfig, int, dMeshBatch, int, int, ArrayView1D<TileTriangleRecord, Stride1D.Dense>, ArrayView1D<int, Stride1D.Dense>> GetFillTileCacheTrianglesKernel()
        {
            if (fillTileCacheTrianglesKernel == null)
            {
                fillTileCacheTrianglesKernel = device.LoadStreamKernel<int, dMeshBatch, int, int, ArrayView1D<TileTriangleRecord, Stride1D.Dense>, ArrayView1D<int, Stride1D.Dense>>(TileCache.PerTriangleFillTileCachesKernel);
            }

            return fillTileCacheTrianglesKernel;
        }

        // this caches the gpu kernel for the clear frame kernel for each shader
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
