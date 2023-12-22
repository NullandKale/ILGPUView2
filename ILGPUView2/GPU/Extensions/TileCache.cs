using ILGPU.Runtime;
using ILGPU;
using System;
using System.Data;
using System.Reflection.Metadata;
using ILGPUView2.GPU.Extensions;

namespace GPU
{
    public class TileCache : IDisposable
    {
        // plan to bitonic sort

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

            perTileTriangleCountArray = gpu.device.Allocate1D<int, Stride1D.Dense>(widthInTiles * heightInTiles, new Stride1D.Dense());
            perTileTriangleRecordArray = gpu.device.Allocate1D<TileTriangleRecord, Stride1D.Dense>(widthInTiles * heightInTiles * RasterisationSettings.maxTrianglesPerTile, new Stride1D.Dense());
        }

        public (ArrayView1D<TileTriangleRecord, Stride1D.Dense> tileTriangleRecords, ArrayView1D<int, Stride1D.Dense> tileTriangleCounts) GetCache()
        {
            perTileTriangleCountArray.MemSetToZero();
            return (perTileTriangleRecordArray, perTileTriangleCountArray);
        }

        public static void TransformTrianglesKernel<TFunc>(int length, dMeshBatch meshes, TFunc filter, int width, int height, ArrayView1D<TileTriangleRecord, Stride1D.Dense> perTileTriangleArray, ArrayView1D<int, Stride1D.Dense> perTileTriangleCount) where TFunc : unmanaged, IVertShader
        {
            var index = Group.DimX * Grid.IdxX + Group.IdxX;

            if (index >= length)
            {
                return;
            }

            // index is the thread index its there is one per triangle
            dMeshTicket currentMesh = meshes.GetMeshTicketByTriangleIndex(index);
            Triangle original = meshes.GetTriangle(index);
            TransformedTriangle transformed = filter.VertShader(original, currentMesh.matrix, width, height);
            transformed.meshID = currentMesh.meshIndex;
            meshes.SetWorkingTriangle(index, transformed);
        }

        public static void PerTriangleFillTileCachesKernel(int length, dMeshBatch meshes, int width, int height, ArrayView1D<TileTriangleRecord, Stride1D.Dense> perTileTriangleArray, ArrayView1D<int, Stride1D.Dense> perTileTriangleCount)
        {
            var index = Group.DimX * Grid.IdxX + Group.IdxX;

            if (index >= length)
            {
                return;
            }

           TransformedTriangle transformed = meshes.GetWorkingTriangle(index);

            if (transformed.stateFlags != 0)
            {
                return;
            }

            // Calculate the tile extents for the transformed triangle
            int tileStartX = (int)Math.Floor(transformed.minX / RasterisationSettings.tileSize);
            int tileStartY = (int)Math.Floor(transformed.minY / RasterisationSettings.tileSize);
            int tileEndX = (int)Math.Ceiling(transformed.maxX / RasterisationSettings.tileSize);
            int tileEndY = (int)Math.Ceiling(transformed.maxY / RasterisationSettings.tileSize);

            int widthInTiles = width / RasterisationSettings.tileSize;
            int heightInTiles = height / RasterisationSettings.tileSize;

            // Adjust tile bounds to include edge tiles
            tileStartX = Math.Max(tileStartX, 0);
            tileStartY = Math.Max(tileStartY, 0);
            tileEndX = Math.Min(tileEndX, widthInTiles);
            tileEndY = Math.Min(tileEndY, heightInTiles);

            // Check if there is no valid tile to process
            if (tileStartX >= widthInTiles || tileStartY >= heightInTiles)
            {
                return;
            }

            // Loop through the tiles and update perTileTriangleArray and perTileTriangleCountArray
            for (int tileY = tileStartY; tileY <= tileEndY; ++tileY)
            {
                for (int tileX = tileStartX; tileX <= tileEndX; ++tileX)
                {
                    Group.Barrier();

                    // Calculate tileIndex based on tile grid
                    int tileIndex = tileY * widthInTiles + tileX;

                    if (tileIndex >= 0 && tileIndex < perTileTriangleCount.Length)
                    {
                        // reserve space for this triangle in the output
                        int currentCount = Atomic.Add(ref perTileTriangleCount[tileIndex], 1);

                        // Check if we've reached the max number of triangles for this tile
                        if (currentCount < RasterisationSettings.maxTrianglesPerTile)
                        {
                            // Calculate the linear index for this tile and triangle within perTileTriangleArray
                            int linearIndex = (tileIndex * RasterisationSettings.maxTrianglesPerTile) + currentCount;

                            perTileTriangleArray[linearIndex] = new TileTriangleRecord(transformed.meshID, meshes.GetLocalIndexByTriangleIndex(transformed.meshID, index), index);
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
