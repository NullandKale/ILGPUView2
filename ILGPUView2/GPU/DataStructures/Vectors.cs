using ILGPU.Algorithms;
using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace GPU
{
    [StructLayout(LayoutKind.Sequential, Size = 16, Pack = 16)]
    public unsafe struct Vec4
    {
        public float x, y, z, w;

        public Vec4(float x, float y, float z, float w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

    }

    [StructLayout(LayoutKind.Sequential, Size = 64, Pack = 64)]
    public unsafe struct Mat4x4
    {
        public fixed float Matrix[16];

        public Vec3 MultiplyVector(Vec3 vec)
        {
            float x = Matrix[0] * vec.x + Matrix[4] * vec.y + Matrix[8] * vec.z + Matrix[12];
            float y = Matrix[1] * vec.x + Matrix[5] * vec.y + Matrix[9] * vec.z + Matrix[13];
            float z = Matrix[2] * vec.x + Matrix[6] * vec.y + Matrix[10] * vec.z + Matrix[14];

            return new Vec3(x, y, z);
        }

        public Vec4 MultiplyVector(Vec4 vec)
        {
            float x = Matrix[0] * vec.x + Matrix[4] * vec.y + Matrix[8] * vec.z + Matrix[12] * vec.w;
            float y = Matrix[1] * vec.x + Matrix[5] * vec.y + Matrix[9] * vec.z + Matrix[13] * vec.w;
            float z = Matrix[2] * vec.x + Matrix[6] * vec.y + Matrix[10] * vec.z + Matrix[14] * vec.w;
            float w = Matrix[3] * vec.x + Matrix[7] * vec.y + Matrix[11] * vec.z + Matrix[15] * vec.w;

            return new Vec4(x, y, z, w);
        }

        public GPU.Triangle MultiplyTriangle(GPU.Triangle tri)
        {
            Vec4 v0 = MultiplyVector(new Vec4(tri.v0.x, tri.v0.y, tri.v0.z, 1.0f));
            Vec4 v1 = MultiplyVector(new Vec4(tri.v1.x, tri.v1.y, tri.v1.z, 1.0f));
            Vec4 v2 = MultiplyVector(new Vec4(tri.v2.x, tri.v2.y, tri.v2.z, 1.0f));

            return new GPU.Triangle(new Vec3(v0.x / v0.w, v0.y / v0.w, v0.z / v0.w),
                                    new Vec3(v1.x / v1.w, v1.y / v1.w, v1.z / v1.w),
                                    new Vec3(v2.x / v2.w, v2.y / v2.w, v2.z / v2.w));
        }

        public static Mat4x4 CreateCameraMatrix(Vec3 cameraPos, Vec3 up, Vec3 lookAt, int outputSizeX, int outputSizeY, float hFovDegrees, float near, float far)
        {
            Mat4x4 vpMatrix = new Mat4x4();

            // Create the projection matrix
            float aspectRatio = (float)outputSizeX / (float)outputSizeY;
            float fov = hFovDegrees * (MathF.PI / 180.0f); // Convert to radians
            float f = 1.0f / MathF.Tan(fov / 2.0f);
            float range = far - near;

            Mat4x4 projectionMatrix = new Mat4x4();
            projectionMatrix.Matrix[0] = f / aspectRatio;
            projectionMatrix.Matrix[5] = f;
            projectionMatrix.Matrix[10] = (far + near) / range;
            projectionMatrix.Matrix[14] = 2.0f * far * near / range;
            projectionMatrix.Matrix[11] = -1.0f;
            projectionMatrix.Matrix[15] = 1.0f;

            // Create the view matrix
            Vec3 zAxis = cameraPos - lookAt; // Forward
            zAxis = zAxis.Normalize();
            Vec3 xAxis = Vec3.cross(up, zAxis); // Right
            xAxis = xAxis.Normalize();
            Vec3 yAxis = Vec3.cross(zAxis, xAxis); // Up

            Mat4x4 viewMatrix = new Mat4x4();
            viewMatrix.Matrix[0] = xAxis.x;
            viewMatrix.Matrix[4] = xAxis.y;
            viewMatrix.Matrix[8] = xAxis.z;
            viewMatrix.Matrix[12] = -Vec3.dot(xAxis, cameraPos);
            viewMatrix.Matrix[1] = yAxis.x;
            viewMatrix.Matrix[5] = yAxis.y;
            viewMatrix.Matrix[9] = yAxis.z;
            viewMatrix.Matrix[13] = -Vec3.dot(yAxis, cameraPos);
            viewMatrix.Matrix[2] = zAxis.x;
            viewMatrix.Matrix[6] = zAxis.y;
            viewMatrix.Matrix[10] = zAxis.z;
            viewMatrix.Matrix[14] = -Vec3.dot(zAxis, cameraPos);
            viewMatrix.Matrix[15] = 1.0f;

            // Combine the view and projection matrices to form the VP matrix
            vpMatrix = projectionMatrix * viewMatrix;

            return vpMatrix;
        }


        public static Mat4x4 CreateModelMatrix(Vec3 pos, Vec3 rotDegrees, Vec3 scale)
        {
            Mat4x4 modelMatrix = new Mat4x4();

            // Create scaling matrix
            Mat4x4 scaleMatrix = new Mat4x4();
            scaleMatrix.Matrix[0] = scale.x;
            scaleMatrix.Matrix[5] = scale.y;
            scaleMatrix.Matrix[10] = scale.z;
            scaleMatrix.Matrix[15] = 1.0f;

            // Create rotation matrix
            float rx = rotDegrees.x * (MathF.PI / 180.0f); // Convert to radians
            float ry = rotDegrees.y * (MathF.PI / 180.0f); // Convert to radians
            float rz = rotDegrees.z * (MathF.PI / 180.0f); // Convert to radians

            Mat4x4 rotationX = new Mat4x4();
            rotationX.Matrix[0] = 1.0f;
            rotationX.Matrix[5] = MathF.Cos(rx);
            rotationX.Matrix[6] = MathF.Sin(rx);
            rotationX.Matrix[9] = -MathF.Sin(rx);
            rotationX.Matrix[10] = MathF.Cos(rx);
            rotationX.Matrix[15] = 1.0f;

            Mat4x4 rotationY = new Mat4x4();
            rotationY.Matrix[0] = MathF.Cos(ry);
            rotationY.Matrix[2] = -MathF.Sin(ry);
            rotationY.Matrix[5] = 1.0f;
            rotationY.Matrix[8] = MathF.Sin(ry);
            rotationY.Matrix[10] = MathF.Cos(ry);
            rotationY.Matrix[15] = 1.0f;

            Mat4x4 rotationZ = new Mat4x4();
            rotationZ.Matrix[0] = MathF.Cos(rz);
            rotationZ.Matrix[1] = MathF.Sin(rz);
            rotationZ.Matrix[4] = -MathF.Sin(rz);
            rotationZ.Matrix[5] = MathF.Cos(rz);
            rotationZ.Matrix[10] = 1.0f;
            rotationZ.Matrix[15] = 1.0f;

            Mat4x4 rotationMatrix = rotationX * rotationY * rotationZ;

            // Create translation matrix
            Mat4x4 translationMatrix = new Mat4x4();
            translationMatrix.Matrix[0] = 1.0f;
            translationMatrix.Matrix[5] = 1.0f;
            translationMatrix.Matrix[10] = 1.0f;
            translationMatrix.Matrix[12] = pos.x;
            translationMatrix.Matrix[13] = pos.y;
            translationMatrix.Matrix[14] = pos.z;
            translationMatrix.Matrix[15] = 1.0f;

            // Combine the matrices to form the model matrix
            modelMatrix = translationMatrix * rotationMatrix * scaleMatrix;

            return modelMatrix;
        }

        public static Mat4x4 operator *(Mat4x4 mat1, Mat4x4 mat2)
        {
            Mat4x4 result = new Mat4x4();

            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    float sum = 0.0f;
                    for (int k = 0; k < 4; k++)
                    {
                        sum += mat1.Matrix[row + k * 4] * mat2.Matrix[k + col * 4];
                    }
                    result.Matrix[row + col * 4] = sum;
                }
            }

            return result;
        }


    }

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

        public float length()
        {
            return XMath.Sqrt(x * x + y * y);
        }
    }

    [StructLayout(LayoutKind.Sequential, Size = 12, Pack = 16)]
    public struct Vec3
    {
        public static readonly Vec3 Ones = new Vec3(1,1,1);
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

        public float average()
        {
            return (x + y + z) / 3.0f;
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


        public static Vec3 reflect(Vec3 incomming, Vec3 normal)
        {
            return incomming - normal * 2f * dot(incomming, normal);
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

        public static Vec3 abs(Vec3 vec3)
        {
            return new Vec3(XMath.Abs(vec3.x), XMath.Abs(vec3.y), XMath.Abs(vec3.z));
        }

        public static Vec3 ceil(Vec3 vec3)
        {
            return new Vec3(XMath.Ceiling(vec3.x), XMath.Ceiling(vec3.y), XMath.Ceiling(vec3.z));
        }

        public static Vec3 mod(Vec3 vec3, float v)
        {
            return vec3 - v * floor(vec3 / v);
            //return new Vec3(vec3.x % v, vec3.y % v, vec3.z % v);
        }

        public static Vec3 clamp(Vec3 vec3, float v1, float v2)
        {
            return new Vec3(XMath.Clamp(vec3.x, v1, v2), XMath.Clamp(vec3.y, v1, v2), XMath.Clamp(vec3.z, v1, v2));
        }

        public static Vec3 floor(Vec3 vec3)
        {
            return new Vec3(XMath.Floor(vec3.x), XMath.Floor(vec3.y), XMath.Floor(vec3.z));
        }

        public static Vec3 vecif(Vec3 val, Vec3 cond, Vec3 newVal)
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
            return new Vec3(XMath.Min(a.x, XMath.Min(a.y, a.z)));
        }

        public static Vec3 Max(Vec3 a)
        {
            return new Vec3(XMath.Max(a.x, XMath.Max(a.y, a.z)));
        }

        public static Vec3 Abs(Vec3 viewLerp)
        {
            return new Vec3(XMath.Abs(viewLerp.x), XMath.Abs(viewLerp.y), XMath.Abs(viewLerp.z));
        }

        public static Vec3 Ceiling(Vec3 v)
        {
            return new Vec3(XMath.Ceiling(v.x), XMath.Ceiling(v.y), XMath.Ceiling(v.z));
        }

        public static Vec3 Clamp(Vec3 modViewLerp, float v1, float v2)
        {
            return new Vec3(
                XMath.Clamp(modViewLerp.x, v1, v2),
                XMath.Clamp(modViewLerp.y, v1, v2),
                XMath.Clamp(modViewLerp.z, v1, v2)
            );
        }

        public static float Mod(Vec3 vec3, float v)
        {
            return vec3.x % v + vec3.y % v + vec3.z % v;
        }

        public bool NearZero()
        {
            const float epsilon = 0.0001f; // Define a small threshold for comparison

            // Check if each component of the vector is close to zero
            return XMath.Abs(x) < epsilon && XMath.Abs(y) < epsilon && XMath.Abs(z) < epsilon;
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


        public static implicit operator Vec4(Vec3 d)
        {
            return new Vec4(d.x, d.y, d.z, 0);
        }

        public static implicit operator Vec3(Vec4 d)
        {
            return new Vec3(d.x, d.y, d.z);
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
