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
using System.Windows.Shapes;
using System.Windows;
using ILGPU.Algorithms;
using System.Windows.Media.Media3D;
using System.Reflection.Metadata.Ecma335;

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

        public RGBA32 FragShader(float x, float y, TransformedTriangle triangle, float i)
        {
            float r = XMath.Sin(i * 15.53f + 0.26f);
            float g = XMath.Sin(i * 156.15f + 0.98f);
            float b = XMath.Sin(i * 3.75f + 0.7612f);

            return new RGBA32(r, g, b);
        }


        public TransformedTriangle VertShader(Triangle original, Mat4x4 matrix, int width, int height)
        {
            Vec4 v0 = matrix.MultiplyVector(new Vec4(original.v0.x, original.v0.y, original.v0.z, 1.0f));
            Vec4 v1 = matrix.MultiplyVector(new Vec4(original.v1.x, original.v1.y, original.v1.z, 1.0f));
            Vec4 v2 = matrix.MultiplyVector(new Vec4(original.v2.x, original.v2.y, original.v2.z, 1.0f));

            return new TransformedTriangle(v0, v1, v2, width, height);
        }
    }

    // this is a single triangle

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
        // this controls how big the tiles are, and directly impacts performance
        // 8 seems to be best for 1080p
        const int tileSize = 8;
        float GetNear();
        float GetFar();
        Mat4x4 GetCameraMat();

        // Fragment shader function
        RGBA32 FragShader(float x, float y, TransformedTriangle triangle, float i);
    }

    // this is the triangle output format, kinda like ther vertex format
    public struct TransformedTriangle
    {
        public Vec3 wTerm;
        public Vec3 v0, v1, v2;
        public float minX, minY, maxX, maxY;
        public float avgDepth;

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

            // Compute and store the average depth
            avgDepth = (this.v0.z + this.v1.z + this.v2.z) / 3.0f;

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
        // separate clear kernel for performance
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

        public static ArrayView1D<int, Stride1D.Dense> PerformHeapSortInline(int linearIndex, int count, ArrayView1D<TileTriangleRecord, Stride1D.Dense> perTileTriangleArray)
        {
            var sortedIndices = LocalMemory.Allocate1D<int>(TileCache.maxTrianglesPerTile);

            // Initialize sortedIndices
            for (int i = 0; i < count; i++)
            {
                sortedIndices[i] = i;
            }

            // Inline heapsort
            for (int i = count / 2 - 1; i >= 0; i--)
            {
                int root = i;
                int child;
                int toSwap = root;

                do
                {
                    child = 2 * root + 1;

                    if (child < count &&
                        perTileTriangleArray[linearIndex + sortedIndices[child]].depth <
                        perTileTriangleArray[linearIndex + sortedIndices[toSwap]].depth)
                    {
                        toSwap = child;
                    }

                    if (child + 1 < count &&
                        perTileTriangleArray[linearIndex + sortedIndices[child + 1]].depth <
                        perTileTriangleArray[linearIndex + sortedIndices[toSwap]].depth)
                    {
                        toSwap = child + 1;
                    }

                    if (toSwap != root)
                    {
                        int temp = sortedIndices[root];
                        sortedIndices[root] = sortedIndices[toSwap];
                        sortedIndices[toSwap] = temp;
                        root = toSwap;
                    }
                    else
                    {
                        break;
                    }

                } while (child < count);
            }

            for (int i = count - 1; i >= 0; i--)
            {
                int temp = sortedIndices[0];
                sortedIndices[0] = sortedIndices[i];
                sortedIndices[i] = temp;

                int root = 0;
                int child;
                int toSwap = root;

                do
                {
                    child = 2 * root + 1;

                    if (child < i &&
                        perTileTriangleArray[linearIndex + sortedIndices[child]].depth <
                        perTileTriangleArray[linearIndex + sortedIndices[toSwap]].depth)
                    {
                        toSwap = child;
                    }

                    if (child + 1 < i &&
                        perTileTriangleArray[linearIndex + sortedIndices[child + 1]].depth <
                        perTileTriangleArray[linearIndex + sortedIndices[toSwap]].depth)
                    {
                        toSwap = child + 1;
                    }

                    if (toSwap != root)
                    {
                        int tempSwap = sortedIndices[root];
                        sortedIndices[root] = sortedIndices[toSwap];
                        sortedIndices[toSwap] = tempSwap;
                        root = toSwap;
                    }
                    else
                    {
                        break;
                    }

                } while (child < i);
            }

            return sortedIndices;
        }


        public static void TriangleImageFilterManyTileCacheKernel<TFunc>(Index1D index, int threadCount, FrameBuffer output, dMeshBatch meshes, ArrayView1D<TileTriangleRecord, Stride1D.Dense> perTileTriangleArray, ArrayView1D<int, Stride1D.Dense> perTileTriangleCount, TFunc filter) where TFunc : unmanaged, ITriangleImageFilterTiled
        {
            int startX = (index.X % (output.width / ITriangleImageFilterTiled.tileSize)) * ITriangleImageFilterTiled.tileSize;
            int startY = (index.X / (output.width / ITriangleImageFilterTiled.tileSize)) * ITriangleImageFilterTiled.tileSize;

            int endX = Math.Min(startX + ITriangleImageFilterTiled.tileSize, output.width);
            int endY = Math.Min(startY + ITriangleImageFilterTiled.tileSize, output.height);

            float near = filter.GetNear();
            float far = filter.GetFar();
            float invFarNearDiff = 1.0f / (far - near);
            float widthFactor = 2.0f / output.width;
            float heightFactor = 2.0f / output.height;

            int linearIndex = index * TileCache.maxTrianglesPerTile;
            int count = perTileTriangleCount[index];
            var sortedIndices = PerformHeapSortInline(linearIndex, count, perTileTriangleArray);

            for (int i = 0; i < count; i++)
            {
                int sortedIndex = sortedIndices[i];
                TileTriangleRecord triangleRecord = perTileTriangleArray[linearIndex + sortedIndex];
                TransformedTriangle workingTriangle = meshes.GetWorkingTriangle(triangleRecord.meshID, triangleRecord.triangleIndex);

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
                            float interpolatedW = alpha * workingTriangle.wTerm.x + beta * workingTriangle.wTerm.y + gamma * workingTriangle.wTerm.z;

                            if (interpolatedW > 0)
                            {
                                float depthValue = alpha * v0.z + beta * v1.z + gamma * v2.z;
                                float currentDepth = output.GetDepth(x, y);

                                if (depthValue >= currentDepth)
                                {
                                    output.SetDepthPixel(x, y, depthValue);

                                    // Recheck the depth condition after updating the depth buffer
                                    currentDepth = output.GetDepth(x, y);
                                    if (depthValue >= currentDepth)
                                    {
                                        RGBA32 color = filter.FragShader(x, y, workingTriangle, (float)triangleRecord.triangleIndex);
                                        output.SetColorAt(x, y, color);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

    }

    public struct TileTriangleRecord
    {
        public int meshID;
        public int triangleIndex;
        public float depth;

        public TileTriangleRecord(int meshID, int triangleIndex, float depth)
        {
            this.meshID = meshID;
            this.triangleIndex = triangleIndex;
            this.depth = depth;
        }
    }

    public class TileCache : IDisposable
    {
        // plan to bitonic sort
        public const int maxTrianglesPerTile = 512;

        private Renderer gpu;
        public int widthInTiles;
        public int heightInTiles;

        private MemoryBuffer1D<TileTriangleRecord, Stride1D.Dense> perTileTriangleRecordArray;
        private MemoryBuffer1D<int, Stride1D.Dense> perTileTriangleCountArray;

        public TileCache(Renderer gpu, int widthInTiles, int heightInTiles)
        {
            this.gpu = gpu;
            this.widthInTiles = widthInTiles;
            this.heightInTiles = heightInTiles;

            // allocate linear memory for 50 TileTriangleRecord per tile
            perTileTriangleCountArray = gpu.device.Allocate1D<int, Stride1D.Dense>(widthInTiles * heightInTiles, new Stride1D.Dense());
            perTileTriangleRecordArray = gpu.device.Allocate1D<TileTriangleRecord, Stride1D.Dense>(widthInTiles * heightInTiles * maxTrianglesPerTile, new Stride1D.Dense());
        }

        public (ArrayView1D<TileTriangleRecord, Stride1D.Dense> tileTriangleRecords, ArrayView1D<int, Stride1D.Dense> tileTriangleCounts) GetCache()
        {
            perTileTriangleCountArray.MemSetToZero();
            return (perTileTriangleRecordArray, perTileTriangleCountArray);
        }

        public static void TransformTrianglesKernel<TFunc>(Index1D index, dMeshBatch meshes, TFunc filter, int width, int height, ArrayView1D<TileTriangleRecord, Stride1D.Dense> perTileTriangleArray, ArrayView1D<int, Stride1D.Dense> perTileTriangleCount) where TFunc : unmanaged, IVertShader
        {
            // index is the thread index its there is one per triangle
            dMeshTicket currentMesh = meshes.GetMeshTicketByTriangleIndex(index);
            Triangle original = meshes.GetTriangle(index);
            TransformedTriangle transformed = filter.VertShader(original, currentMesh.matrix, width, height);
            meshes.SetWorkingTriangle(index, transformed);

            if (transformed.stateFlags != 0)
            {
                return;
            }

            // Calculate the tile extents for the transformed triangle
            int tileStartX = (int)Math.Floor(transformed.minX / ITriangleImageFilterTiled.tileSize);
            int tileStartY = (int)Math.Floor(transformed.minY / ITriangleImageFilterTiled.tileSize);
            int tileEndX = (int)Math.Ceiling(transformed.maxX / ITriangleImageFilterTiled.tileSize);
            int tileEndY = (int)Math.Ceiling(transformed.maxY / ITriangleImageFilterTiled.tileSize);

            // Loop through the tiles and update perTileTriangleArray and perTileTriangleCountArray
            for (int tileY = tileStartY; tileY < tileEndY; ++tileY)
            {
                for (int tileX = tileStartX; tileX < tileEndX; ++tileX)
                {
                    // Calculate the number of tiles in the x and y dimensions
                    int widthInTiles = width / ITriangleImageFilterTiled.tileSize;
                    int heightInTiles = height / ITriangleImageFilterTiled.tileSize;

                    // Calculate tileIndex based on tile grid
                    int tileIndex = tileY * widthInTiles + tileX;

                    if (tileIndex >= 0 && tileIndex < perTileTriangleCount.Length)
                    {
                        int currentCount = Atomic.Add(ref perTileTriangleCount[tileIndex], 1);

                        // Check if we've reached the max number of triangles for this tile
                        if (currentCount < TileCache.maxTrianglesPerTile)
                        {
                            // Calculate the linear index for this tile and triangle within perTileTriangleArray
                            int linearIndex = tileIndex * TileCache.maxTrianglesPerTile + currentCount;

                            perTileTriangleArray[linearIndex] = new TileTriangleRecord(currentMesh.meshIndex, meshes.GetLocalIndexByTriangleIndex(index), transformed.avgDepth);
                        }
                    }
                }
            }

        }


        public void Dispose()
        {
            if(perTileTriangleCountArray != null)
            {
                perTileTriangleCountArray.Dispose();
            }

            if (perTileTriangleRecordArray != null)
            {
                perTileTriangleRecordArray.Dispose();
            }
        }
    }


    public partial class Renderer
    {
        private TileCache tileCache;

        // this function draws multiple meshes to the output with the shader ITriangleImageFilterTiled on the gpu
        public void ExecuteTriangleFilterMany<TFunc>(GPUFrameBuffer output, GPUMeshBatch meshes, TFunc filter = default) where TFunc : unmanaged, ITriangleImageFilterTiled
        {
            int widthInTiles = output.width / ITriangleImageFilterTiled.tileSize;
            int heightInTiles = output.height / ITriangleImageFilterTiled.tileSize;
            int numTiles = (widthInTiles) * (heightInTiles);

            if(tileCache == null)
            {
                tileCache = new TileCache(this, widthInTiles, heightInTiles);
            }
            else
            {
                if(tileCache.heightInTiles != heightInTiles || tileCache.widthInTiles != widthInTiles)
                {
                    tileCache.Dispose();
                    tileCache = new TileCache(this, widthInTiles, heightInTiles);
                }
            }

            var transformKernel = GetTileCacheTransformTrianglesKernel(filter);
            var drawKernel = GetTriangleImageFilterManyTileCacheKernel(filter);
            var clearKernel = GetClearFrameBufferKernel(filter);

            FrameBuffer framebuffer = output.toDevice(this);

            (ArrayView1D<TileTriangleRecord, Stride1D.Dense> tileTriangles, ArrayView1D<int, Stride1D.Dense > tileTriangleCounts) = tileCache.GetCache();
            
            meshes.ApplyCamera(filter.GetCameraMat());
            dMeshBatch deviceMeshes = meshes.toGPU(this);

            clearKernel(numTiles, framebuffer, filter);
            transformKernel(deviceMeshes.triangles.IntLength, deviceMeshes, filter, output.width, output.height, tileTriangles, tileTriangleCounts);
            drawKernel(numTiles, numTiles, framebuffer, deviceMeshes, tileTriangles, tileTriangleCounts, filter);
        }


        // this caches the gpu kernel for the vert shader kernel for each shader
        private Action<Index1D, int, FrameBuffer, dMeshBatch, ArrayView1D<TileTriangleRecord, Stride1D.Dense>, ArrayView1D<int, Stride1D.Dense>, TFunc> GetTriangleImageFilterManyTileCacheKernel<TFunc>(TFunc filter = default) where TFunc : unmanaged, ITriangleImageFilterTiled
        {
            if (!kernels.ContainsKey(filter.GetType()))
            {
                var kernel = device.LoadAutoGroupedStreamKernel<Index1D, int, FrameBuffer, dMeshBatch, ArrayView1D<TileTriangleRecord, Stride1D.Dense>, ArrayView1D<int, Stride1D.Dense>, TFunc>(TriangleImageFilterManyTileCacheKernel);
                kernels.Add(filter.GetType(), kernel);
            }

            return (Action<Index1D, int, FrameBuffer, dMeshBatch, ArrayView1D<TileTriangleRecord, Stride1D.Dense>, ArrayView1D<int, Stride1D.Dense>, TFunc>)kernels[filter.GetType()];
        }

        private Action<Index1D, dMeshBatch, TFunc, int, int, ArrayView1D<TileTriangleRecord, Stride1D.Dense>, ArrayView1D<int, Stride1D.Dense>> GetTileCacheTransformTrianglesKernel<TFunc>(TFunc filter = default) where TFunc : unmanaged, IVertShader
        {

            if (!kernels.ContainsKey(typeof(TileCache)))
            {
                var kernel = device.LoadAutoGroupedStreamKernel<Index1D, dMeshBatch, TFunc, int, int, ArrayView1D<TileTriangleRecord, Stride1D.Dense>, ArrayView1D<int, Stride1D.Dense>>(TileCache.TransformTrianglesKernel);
                kernels.Add(typeof(TileCache), kernel);
            }

            return (Action<Index1D, dMeshBatch, TFunc, int, int, ArrayView1D<TileTriangleRecord, Stride1D.Dense>, ArrayView1D<int, Stride1D.Dense>>)kernels[typeof(TileCache)];
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
