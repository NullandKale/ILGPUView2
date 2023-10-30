namespace GPU
{
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
}
