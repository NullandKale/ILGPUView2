using ILGPU.Algorithms;
using System;
using System.Numerics;

namespace GPU
{
    public struct Vec2
    {
        public float x;
        public float y;

        public Vec2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        public static Vec2 operator +(Vec2 v1, Vec2 v2)
        {
            return new Vec2(v1.x + v2.x, v1.y + v2.y);
        }


        public static Vec2 operator -(Vec2 v1, Vec2 v2)
        {
            return new Vec2(v1.x - v2.x, v1.y - v2.y);
        }


        public static Vec2 operator *(Vec2 v1, Vec2 v2)
        {
            return new Vec2(v1.x * v2.x, v1.y * v2.y);
        }


        public static Vec2 operator /(Vec2 v1, Vec2 v2)
        {
            return new Vec2(v1.x / v2.x, v1.y / v2.y);
        }


        public static Vec2 operator /(float v, Vec2 v1)
        {
            return new Vec2(v / v1.x, v / v1.y);
        }


        public static Vec2 operator *(Vec2 v1, float v)
        {
            return new Vec2(v1.x * v, v1.y * v);
        }


        public static Vec2 operator *(float v, Vec2 v1)
        {
            return new Vec2(v1.x * v, v1.y * v);
        }


        public static Vec2 operator +(Vec2 v1, float v)
        {
            return new Vec2(v1.x + v, v1.y + v);
        }

        public static Vec2 operator -(Vec2 v1, float v)
        {
            return new Vec2(v1.x - v, v1.y - v);
        }

        public static Vec2 operator +(float v, Vec2 v1)
        {
            return new Vec2(v1.x + v, v1.y + v);
        }

        public static Vec2 operator -(float v, Vec2 v1)
        {
            return new Vec2(v1.x - v, v1.y - v);
        }


        public static Vec2 operator /(Vec2 v1, float v)
        {
            return v1 * (1.0f / v);
        }

        public float Dot(Vec2 other)
        {
            return (x * other.x) + (y * other.y);
        }

        public static float Dot(Vec2 a, Vec2 b)
        {
            return a.x * b.x + a.y * b.y;
        }

        public static float Distance(Vec2 point, Vec2 center)
        {
            float dx = point.x - center.x;
            float dy = point.y - center.y;
            return XMath.Sqrt(dx * dx + dy * dy);
        }

    }


    public struct Vec3
    {
        public float x;
        public float y;
        public float z;

        public Vec3(Vec3 toCopy)
        {
            this.x = toCopy.x;
            this.y = toCopy.y;
            this.z = toCopy.z;
        }

        public Vec3(RGBA32 color)
        {
            x = color.r / 255f;
            y = color.g / 255f;
            z = color.b / 255f;
        }

        public Vec3(float v)
        {
            this.x = v;
            this.y = v;
            this.z = v;
        }

        public Vec3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public Vec3(double x, double y, double z)
        {
            this.x = (float)x;
            this.y = (float)y;
            this.z = (float)z;
        }

        public Vec3(Vec2 uv_in, float v)
        {
            this.x = uv_in.x;
            this.y = uv_in.y;
            this.z = v;
        }

        public override string ToString()
        {
            return "{" + string.Format("{0:0.00}", x) + ", " + string.Format("{0:0.00}", y) + ", " + string.Format("{0:0.00}", z) + "}";
        }


        public static Vec3 operator -(Vec3 vec)
        {
            return new Vec3(-vec.x, -vec.y, -vec.z);
        }


        public float length()
        {
            return XMath.Sqrt(x * x + y * y + z * z);
        }


        public float lengthSquared()
        {
            return x * x + y * y + z * z;
        }

        public float getAt(int a)
        {
            switch (a)
            {
                case 0:
                    return x;
                case 1:
                    return y;
                case 2:
                    return z;
                default:
                    return 0;
            }
        }

        public float this[int i]
        {
            get
            {
                if (i == 0) return x;
                if (i == 1) return y;
                if (i == 2) return z;
                return 0;
            }
            set
            {
                if (i == 0) x = value;
                else if (i == 1) y = value;
                else if (i == 2) z = value;
            }
        }

        public static Vec3 HsbToRgb(Vec3 hsb)
        {
            float chroma = hsb.z * hsb.y;
            float hue2 = hsb.x * 6f;
            float x = chroma * (1f - Math.Abs(hue2 % 2f - 1f));
            float r, g, b;

            if (hue2 < 1f)
            {
                r = chroma;
                g = x;
                b = 0f;
            }
            else if (hue2 < 2f)
            {
                r = x;
                g = chroma;
                b = 0f;
            }
            else if (hue2 < 3f)
            {
                r = 0f;
                g = chroma;
                b = x;
            }
            else if (hue2 < 4f)
            {
                r = 0f;
                g = x;
                b = chroma;
            }
            else if (hue2 < 5f)
            {
                r = x;
                g = 0f;
                b = chroma;
            }
            else
            {
                r = chroma;
                g = 0f;
                b = x;
            }

            float m = hsb.z - chroma;
            return new Vec3(r + m, g + m, b + m);
        }

        public static Vec3 lerp(Vec3 a, Vec3 b, float t)
        {
            return (1f - t) * a + t * b;
        }

        public static Vec3 setX(Vec3 v, float x)
        {
            return new Vec3(x, v.y, v.z);
        }

        public static Vec3 setY(Vec3 v, float y)
        {
            return new Vec3(v.x, y, v.z);
        }

        public static Vec3 setZ(Vec3 v, float z)
        {
            return new Vec3(v.x, v.y, z);
        }


        public static float dist(Vec3 v1, Vec3 v2)
        {
            float dx = v1.x - v2.x;
            float dy = v1.y - v2.y;
            float dz = v1.z - v2.z;
            return XMath.Sqrt(dx * dx + dy * dy + dz * dz);
        }


        public static Vec3 operator +(Vec3 v1, Vec3 v2)
        {
            return new Vec3(v1.x + v2.x, v1.y + v2.y, v1.z + v2.z);
        }


        public static Vec3 operator -(Vec3 v1, Vec3 v2)
        {
            return new Vec3(v1.x - v2.x, v1.y - v2.y, v1.z - v2.z);
        }


        public static Vec3 operator *(Vec3 v1, Vec3 v2)
        {
            return new Vec3(v1.x * v2.x, v1.y * v2.y, v1.z * v2.z);
        }


        public static Vec3 operator /(Vec3 v1, Vec3 v2)
        {
            return new Vec3(v1.x / v2.x, v1.y / v2.y, v1.z / v2.z);
        }


        public static Vec3 operator /(float v, Vec3 v1)
        {
            return new Vec3(v / v1.x, v / v1.y, v / v1.z);
        }


        public static Vec3 operator *(Vec3 v1, float v)
        {
            return new Vec3(v1.x * v, v1.y * v, v1.z * v);
        }


        public static Vec3 operator *(float v, Vec3 v1)
        {
            return new Vec3(v1.x * v, v1.y * v, v1.z * v);
        }


        public static Vec3 operator +(Vec3 v1, float v)
        {
            return new Vec3(v1.x + v, v1.y + v, v1.z + v);
        }


        public static Vec3 operator +(float v, Vec3 v1)
        {
            return new Vec3(v1.x + v, v1.y + v, v1.z + v);
        }

        public static Vec3 operator -(Vec3 v1, float v)
        {
            return new Vec3(v1.x + v, v1.y + v, v1.z + v);
        }


        public static Vec3 operator -(float v, Vec3 v1)
        {
            return new Vec3(v1.x + v, v1.y + v, v1.z + v);
        }


        public static Vec3 operator /(Vec3 v1, float v)
        {
            return v1 * (1.0f / v);
        }


        public static float dot(Vec3 v1, Vec3 v2)
        {
            return v1.x * v2.x + v1.y * v2.y + v1.z * v2.z;
        }


        public static Vec3 cross(Vec3 v1, Vec3 v2)
        {
            return new Vec3(v1.y * v2.z - v1.z * v2.y,
                          -(v1.x * v2.z - v1.z * v2.x),
                            v1.x * v2.y - v1.y * v2.x);
        }


        public static Vec3 unitVector(Vec3 v)
        {
            return v / XMath.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
        }

        public Vec3 Normalize()
        {
            return unitVector(this);
        }


        public static Vec3 reflect(Vec3 normal, Vec3 incomming)
        {
            return unitVector(incomming - normal * 2f * dot(incomming, normal));
        }


        public static Vec3 refract(Vec3 v, Vec3 n, float niOverNt)
        {
            Vec3 uv = unitVector(v);
            float dt = dot(uv, n);
            float discriminant = 1.0f - niOverNt * niOverNt * (1f - dt * dt);

            if (discriminant > 0)
            {
                return niOverNt * (uv - (n * dt)) - n * XMath.Sqrt(discriminant);
            }

            return v;
        }


        public static float NormalReflectance(Vec3 normal, Vec3 incomming, float iorFrom, float iorTo)
        {
            float iorRatio = iorFrom / iorTo;
            float cosThetaI = -dot(normal, incomming);
            float sinThetaTSquared = iorRatio * iorRatio * (1 - cosThetaI * cosThetaI);
            if (sinThetaTSquared > 1)
            {
                return 1f;
            }

            float cosThetaT = XMath.Sqrt(1 - sinThetaTSquared);
            float rPerpendicular = (iorFrom * cosThetaI - iorTo * cosThetaT) / (iorFrom * cosThetaI + iorTo * cosThetaT);
            float rParallel = (iorFrom * cosThetaI - iorTo * cosThetaT) / (iorFrom * cosThetaI + iorTo * cosThetaT);
            return (rPerpendicular * rPerpendicular + rParallel * rParallel) / 2f;
        }


        public static Vec3 aces_approx(Vec3 v)
        {
            v *= 0.6f;
            float a = 2.51f;
            float b = 0.03f;
            float c = 2.43f;
            float d = 0.59f;
            float e = 0.14f;
            Vec3 working = (v * (a * v + b)) / (v * (c * v + d) + e);
            return new Vec3(XMath.Clamp(working.x, 0, 1), XMath.Clamp(working.y, 0, 1), XMath.Clamp(working.z, 0, 1));
        }


        public static Vec3 reinhard(Vec3 v)
        {
            return v / (1.0f + v);
        }


        public static bool Equals(Vec3 a, Vec3 b)
        {
            return a.x == b.x &&
                   a.y == b.y &&
                   a.z == b.z;
        }

        public static int CompareTo(Vec3 a, Vec3 b)
        {
            return a.lengthSquared().CompareTo(b.lengthSquared());
        }

        internal static Vec3 abs(Vec3 vec3)
        {
            return new Vec3(XMath.Abs(vec3.x), XMath.Abs(vec3.y), XMath.Abs(vec3.z));
        }

        internal static Vec3 ceil(Vec3 vec3)
        {
            return new Vec3(XMath.Ceiling(vec3.x), XMath.Ceiling(vec3.y), XMath.Ceiling(vec3.z));
        }

        internal static Vec3 mod(Vec3 vec3, float v)
        {
            return vec3 - v * floor(vec3 / v);
            //return new Vec3(vec3.x % v, vec3.y % v, vec3.z % v);
        }

        internal static Vec3 clamp(Vec3 vec3, float v1, float v2)
        {
            return new Vec3(XMath.Clamp(vec3.x, v1, v2), XMath.Clamp(vec3.y, v1, v2), XMath.Clamp(vec3.z, v1, v2));
        }

        internal static Vec3 floor(Vec3 vec3)
        {
            return new Vec3(XMath.Floor(vec3.x), XMath.Floor(vec3.y), XMath.Floor(vec3.z));
        }

        internal static Vec3 vecif(Vec3 val, Vec3 cond, Vec3 newVal)
        {
            Vec3 toReturn = val;

            if ((int)val.x == (int)cond.x)
            {
                toReturn.x = newVal.x;
            }

            if ((int)val.y == (int)cond.y)
            {
                toReturn.y = newVal.y;
            }

            if ((int)val.z == (int)cond.z)
            {
                toReturn.z = newVal.z;
            }

            return toReturn;
        }

        public static Vec3 Clamp(Vec3 value, Vec3 min, Vec3 max)
        {
            float x = XMath.Clamp(value.x, min.x, max.x);
            float y = XMath.Clamp(value.y, min.y, max.y);
            float z = XMath.Clamp(value.z, min.z, max.z);
            return new Vec3(x, y, z);
        }

        public static Vec3 Pow(Vec3 color, float power)
        {
            return new Vec3(
                XMath.Pow(color.x, power),
                XMath.Pow(color.y, power),
                XMath.Pow(color.z, power)
            );
        }

        public static Vec3 Log(Vec3 color, float base_value)
        {
            return new Vec3(
                XMath.Log(color.x, base_value),
                XMath.Log(color.y, base_value),
                XMath.Log(color.z, base_value)
            );
        }

        public static Vec3 Sqrt(Vec3 vec3)
        {
            return new Vec3(
                XMath.Sqrt(vec3.x),
                XMath.Sqrt(vec3.y),
                XMath.Sqrt(vec3.z)
            );
        }

        public static Vec3 Exp(Vec3 vec3)
        {
            return new Vec3(
                XMath.Exp(vec3.x),
                XMath.Exp(vec3.y),
                XMath.Exp(vec3.z)
            );
        }

        public static Vec3 Min(Vec3 a, Vec3 b)
        {
            return new Vec3(
                XMath.Min(a.x, b.x),
                XMath.Min(a.y, b.y),
                XMath.Min(a.z, b.z)
            );
        }

        public static Vec3 Max(Vec3 a, Vec3 b)
        {
            return new Vec3(
                XMath.Max(a.x, b.x),
                XMath.Max(a.y, b.y),
                XMath.Max(a.z, b.z)
            );
        }

        public static Vec3 Min(Vec3 a)
        {
            return new Vec3(XMath.Min(a.x, XMath.Min(a.y, a.z)) );
        }

        public static Vec3 Max(Vec3 a)
        {
            return new Vec3(XMath.Max(a.x, XMath.Max(a.y, a.z))
            );
        }

        public static object Abs(Vec3 viewLerp)
        {
            throw new NotImplementedException();
        }

        public static Vec3 Ceiling(object v)
        {
            throw new NotImplementedException();
        }

        public static Vec3 Clamp(Vec3 modViewLerp, float v1, float v2)
        {
            throw new NotImplementedException();
        }

        public static float Mod(Vec3 vec3, int v)
        {
            throw new NotImplementedException();
        }

        public static implicit operator Vector3(Vec3 d)
        {
            return new Vector3((float)d.x, (float)d.y, (float)d.z);
        }

        public static implicit operator Vec3(Vector3 d)
        {
            return new Vec3(d.X, d.Y, d.Z);
        }

        public static implicit operator Vector4(Vec3 d)
        {
            return new Vector4((float)d.x, (float)d.y, (float)d.z, 0);
        }

        public static implicit operator Vec3(Vector4 d)
        {
            return new Vec3(d.X, d.Y, d.Z);
        }
    }

    public struct Vec3i
    {
        public int x;
        public int y;
        public int z;

        public Vec3i(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public Vec3i(float x, float y, float z)
        {
            this.x = (int)x;
            this.y = (int)y;
            this.z = (int)z;
        }

        public override string ToString()
        {
            return "{" + string.Format("{0:0.00}", x) + ", " + string.Format("{0:0.00}", y) + ", " + string.Format("{0:0.00}", z) + "}";
        }


        public static Vec3i operator -(Vec3i vec)
        {
            return new Vec3i(-vec.x, -vec.y, -vec.z);
        }


        public float length()
        {
            return XMath.Sqrt(x * x + y * y + z * z);
        }


        public float lengthSquared()
        {
            return x * x + y * y + z * z;
        }

        public float getAt(int a)
        {
            switch (a)
            {
                case 0:
                    return x;
                case 1:
                    return y;
                case 2:
                    return z;
                default:
                    return 0;
            }
        }

        public static Vec3i setX(Vec3i v, int x)
        {
            return new Vec3i(x, v.y, v.z);
        }

        public static Vec3i setY(Vec3i v, int y)
        {
            return new Vec3i(v.x, y, v.z);
        }

        public static Vec3i setZ(Vec3i v, int z)
        {
            return new Vec3i(v.x, v.y, z);
        }


        public static float dist(Vec3i v1, Vec3i v2)
        {
            float dx = v1.x - v2.x;
            float dy = v1.y - v2.y;
            float dz = v1.z - v2.z;
            return XMath.Sqrt(dx * dx + dy * dy + dz * dz);
        }


        public static Vec3i operator +(Vec3i v1, Vec3i v2)
        {
            return new Vec3i(v1.x + v2.x, v1.y + v2.y, v1.z + v2.z);
        }


        public static Vec3i operator -(Vec3i v1, Vec3i v2)
        {
            return new Vec3i(v1.x - v2.x, v1.y - v2.y, v1.z - v2.z);
        }


        public static Vec3i operator *(Vec3i v1, Vec3i v2)
        {
            return new Vec3i(v1.x * v2.x, v1.y * v2.y, v1.z * v2.z);
        }


        public static Vec3i operator /(Vec3i v1, Vec3i v2)
        {
            return new Vec3i(v1.x / v2.x, v1.y / v2.y, v1.z / v2.z);
        }


        public static Vec3i operator /(int v, Vec3i v1)
        {
            return new Vec3i(v / v1.x, v / v1.y, v / v1.z);
        }


        public static Vec3i operator *(Vec3i v1, int v)
        {
            return new Vec3i(v1.x * v, v1.y * v, v1.z * v);
        }


        public static Vec3i operator *(int v, Vec3i v1)
        {
            return new Vec3i(v1.x * v, v1.y * v, v1.z * v);
        }


        public static Vec3i operator +(Vec3i v1, int v)
        {
            return new Vec3i(v1.x + v, v1.y + v, v1.z + v);
        }


        public static Vec3i operator +(int v, Vec3i v1)
        {
            return new Vec3i(v1.x + v, v1.y + v, v1.z + v);
        }


        public static Vec3i operator /(Vec3i v1, int v)
        {
            return v1 * (1 / v);
        }


        public static float dot(Vec3i v1, Vec3i v2)
        {
            return v1.x * v2.x + v1.y * v2.y + v1.z * v2.z;
        }


        public static Vec3i cross(Vec3i v1, Vec3i v2)
        {
            return new Vec3i(v1.y * v2.z - v1.z * v2.y,
                          -(v1.x * v2.z - v1.z * v2.x),
                            v1.x * v2.y - v1.y * v2.x);
        }


        public static bool Equals(Vec3i a, Vec3i b)
        {
            return a.x == b.x &&
                   a.y == b.y &&
                   a.z == b.z;
        }
    }
}
