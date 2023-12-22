namespace GPU
{
    public struct TileTriangleRecord
    {
        public int meshID;
        public int triangleIndex;
        public int globalTriangleIndex;

        public TileTriangleRecord(int meshID, int triangleIndex, int globalTriangleIndex)
        {
            this.meshID = meshID;
            this.triangleIndex = triangleIndex;
            this.globalTriangleIndex = globalTriangleIndex;
        }
    }
}
