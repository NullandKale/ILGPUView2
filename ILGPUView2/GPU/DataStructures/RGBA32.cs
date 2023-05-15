using System;
using System.Runtime.InteropServices;

namespace GPU
{
    [StructLayout(LayoutKind.Sequential, Size = 4, Pack = 4)]
    public struct RGBA32
    {
        public byte a;
        public byte g;
        public byte b;
        public byte r;

        public unsafe RGBA32(int value)
        {
            int* valuePtr = &value;
            byte* bytePtr = (byte*)valuePtr;
            r = bytePtr[0];
            g = bytePtr[1];
            b = bytePtr[2];
            a = bytePtr[3];
        }

        public unsafe int ToInt()
        {
            int result;
            int* resultPtr = &result;
            byte* bytePtr = (byte*)resultPtr;
            bytePtr[0] = r;
            bytePtr[1] = g;
            bytePtr[2] = b;
            bytePtr[3] = a;
            return result;
        }

        public RGBA32()
        {
            r = 0;
            g = 0;
            b = 0;
            a = 255;
        }

        public RGBA32(Vec3 col)
        {
            r = (byte)(col.z * 255);
            g = (byte)(col.y * 255);
            b = (byte)(col.x * 255);
            a = 255;
        }

        public RGBA32(float x, float y, float z)
        {
            r = (byte)(x * 255);
            g = (byte)(y * 255);
            b = (byte)(z * 255);
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

        public Vec3 toVec3()
        {
            return new Vec3(r / 255f, g / 255f, b / 255f);
        }

        public static RGBA32 Lerp(RGBA32 a, RGBA32 b, float t)
        {
            return new RGBA32(
                (byte)(a.r + t * (b.r - a.r)),
                (byte)(a.g + t * (b.g - a.g)),
                (byte)(a.b + t * (b.b - a.b)),
                (byte)(a.a + t * (b.a - a.a))
            );
        }

    }
}
