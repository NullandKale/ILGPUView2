using ILGPU.Runtime;
using ILGPU;
using System;

namespace GPU
{
    public class TileCache : IDisposable
    {
        // plan to bitonic sort
        public const int maxTrianglesPerTile = 512;

        private Renderer gpu;
        public int widthInTiles;
        public int heightInTiles;

        private MemoryBuffer1D<TileTriangleRecord, Stride1D.Dense> perTileTriangleRecordArray;
        private MemoryBuffer1D<int, Stride1D.Dense> perTileTriangleCountArray;
        private MemoryBuffer1D<ushort, Stride1D.Dense> perTileSortedIndiciesArray;

        public TileCache(Renderer gpu, int widthInTiles, int heightInTiles)
        {
            this.gpu = gpu;
            this.widthInTiles = widthInTiles;
            this.heightInTiles = heightInTiles;

            perTileTriangleCountArray = gpu.device.Allocate1D<int, Stride1D.Dense>(widthInTiles * heightInTiles, new Stride1D.Dense());
            perTileSortedIndiciesArray = gpu.device.Allocate1D<ushort, Stride1D.Dense>(widthInTiles * heightInTiles * maxTrianglesPerTile, new Stride1D.Dense());
            perTileTriangleRecordArray = gpu.device.Allocate1D<TileTriangleRecord, Stride1D.Dense>(widthInTiles * heightInTiles * maxTrianglesPerTile, new Stride1D.Dense());
        }

        public (ArrayView1D<TileTriangleRecord, Stride1D.Dense> tileTriangleRecords, ArrayView1D<int, Stride1D.Dense> tileTriangleCounts, ArrayView1D<ushort, Stride1D.Dense>) GetCache()
        {
            perTileTriangleCountArray.MemSetToZero();
            perTileSortedIndiciesArray.MemSetToZero();
            return (perTileTriangleRecordArray, perTileTriangleCountArray, perTileSortedIndiciesArray);
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
}
