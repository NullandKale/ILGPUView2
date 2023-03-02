using System.Runtime.InteropServices;

namespace GPU
{
    [StructLayout(LayoutKind.Sequential, Size = 4)]
    public struct RGBA32
    {
        public byte r;
        public byte g;
        public byte b;
        public byte a;

        public RGBA32(int value)
        {
            r = (byte)(value >> 24);
            g = (byte)(value >> 16);
            b = (byte)(value >> 8);
            a = (byte)(value);
        }

        public RGBA32(Vec3 col)
        {
            r = (byte)(col.x * 255);
            g = (byte)(col.y * 255);
            b = (byte)(col.z * 255);
            a = 255;
        }

        public RGBA32(float x)
        {
            r = (byte)(x * 255);
            g = (byte)(x * 255);
            b = (byte)(x * 255);
            a = 255;
        }

        public RGBA32(byte r, byte g, byte b, byte a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public int ToInt()
        {
            return (r << 24) | (g << 16) | (b << 8) | a;
        }

        public Vec3 toVec3()
        {
            return new Vec3(r / 255f, g / 255f, b / 255f);
        }
    }
}
