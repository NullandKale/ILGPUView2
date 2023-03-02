namespace GPU.RT
{
    public struct Light
    {
        public Vec3 center;
        public float intensity;
        public Vec3 color;
        public float shadowFactor;

        public Light(Vec3 center, Vec3 color, float intensity, float shadowFactor)
        {
            this.center = center;
            this.color = color;
            this.intensity = intensity;
            this.shadowFactor = shadowFactor;
        }
    }
}
