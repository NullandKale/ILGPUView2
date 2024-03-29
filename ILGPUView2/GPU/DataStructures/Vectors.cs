﻿using ILGPU.Algorithms;
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

        public static Vec4 operator +(Vec4 v1, Vec4 v2)
        {
            return new Vec4(v1.x + v2.x, v1.y + v2.y, v1.z + v2.z, v1.w + v2.w);
        }

        public static Vec4 operator -(Vec4 v1, Vec4 v2)
        {
            return new Vec4(v1.x - v2.x, v1.y - v2.y, v1.z - v2.z, v1.w - v2.w);
        }

        public static Vec4 operator *(Vec4 v1, Vec4 v2)
        {
            return new Vec4(v1.x * v2.x, v1.y * v2.y, v1.z * v2.z, v1.w * v2.w);
        }

        public static Vec4 operator /(Vec4 v1, Vec4 v2)
        {
            return new Vec4(v1.x / v2.x, v1.y / v2.y, v1.z / v2.z, v1.w / v2.w);
        }

        public static Vec4 operator /(float v, Vec4 v1)
        {
            return new Vec4(v / v1.x, v / v1.y, v / v1.z, v / v1.w);
        }

        public static Vec4 operator *(Vec4 v1, float v)
        {
            return new Vec4(v1.x * v, v1.y * v, v1.z * v, v1.w * v);
        }

        public static Vec4 operator *(float v, Vec4 v1)
        {
            return new Vec4(v1.x * v, v1.y * v, v1.z * v, v1.w * v);
        }

        public static Vec4 operator +(Vec4 v1, float v)
        {
            return new Vec4(v1.x + v, v1.y + v, v1.z + v, v1.w + v);
        }

        public static Vec4 operator +(float v, Vec4 v1)
        {
            return new Vec4(v1.x + v, v1.y + v, v1.z + v, v1.w + v);
        }

        public static Vec4 operator -(Vec4 v1, float v)
        {
            return new Vec4(v1.x - v, v1.y - v, v1.z - v, v1.w - v);
        }

        public static Vec4 operator -(float v, Vec4 v1)
        {
            return new Vec4(v - v1.x, v - v1.y, v - v1.z, v - v1.w);
        }

        public static Vec4 operator /(Vec4 v1, float v)
        {
            return v1 * (1.0f / v);
        }

        public static Vec4 Normalize(Vec4 v)
        {
            return v / XMath.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z + v.w * v.w);
        }

        public Vec3 xyz()
        {
            return new Vec3(x, y, z);
        }
    }

    public struct Mat4x4
    {
        // for some reason a fixed array causes weird cashes
        public float d0, d1, d2, d3, d4, d5, d6, d7, d8, d9, d10, d11, d12, d13, d14, d15;

        public float Get(int index)
        {
            switch (index)
            {
                case 0: return d0;
                case 1: return d1;
                case 2: return d2;
                case 3: return d3;
                case 4: return d4;
                case 5: return d5;
                case 6: return d6;
                case 7: return d7;
                case 8: return d8;
                case 9: return d9;
                case 10: return d10;
                case 11: return d11;
                case 12: return d12;
                case 13: return d13;
                case 14: return d14;
                case 15: return d15;
                default: return 0.0f; // can't throw exceptions or print on the GPU
            }
        }

        public void Set(int index, float value)
        {
            switch (index)
            {
                case 0: d0 = value; break;
                case 1: d1 = value; break;
                case 2: d2 = value; break;
                case 3: d3 = value; break;
                case 4: d4 = value; break;
                case 5: d5 = value; break;
                case 6: d6 = value; break;
                case 7: d7 = value; break;
                case 8: d8 = value; break;
                case 9: d9 = value; break;
                case 10: d10 = value; break;
                case 11: d11 = value; break;
                case 12: d12 = value; break;
                case 13: d13 = value; break;
                case 14: d14 = value; break;
                case 15: d15 = value; break;
                default: break; // can't throw exceptions or print on the GPU
            }
        }

        public Vec4 MultiplyVector(Vec4 vec)
        {
            float x = d0 * vec.x + d4 * vec.y + d8 * vec.z + d12 * vec.w;
            float y = d1 * vec.x + d5 * vec.y + d9 * vec.z + d13 * vec.w;
            float z = d2 * vec.x + d6 * vec.y + d10 * vec.z + d14 * vec.w;
            float w = d3 * vec.x + d7 * vec.y + d11 * vec.z + d15 * vec.w;

            return new Vec4(x, y, z, w);
        }

        public Vec3 MultiplyVector(Vec3 vec)
        {
            float x = d0 * vec.x + d4 * vec.y + d8 * vec.z + d12;
            float y = d1 * vec.x + d5 * vec.y + d9 * vec.z + d13;
            float z = d2 * vec.x + d6 * vec.y + d10 * vec.z + d14;

            return new Vec3(x, y, z);
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
            float fov = hFovDegrees * (MathF.PI / 180.0f);
            float f = 1.0f / MathF.Tan(fov / 2.0f);
            float range = far - near;

            Mat4x4 projectionMatrix = new Mat4x4();
            projectionMatrix.Set(0, f / aspectRatio);
            projectionMatrix.Set(5, f);
            projectionMatrix.Set(10, (far + near) / range);
            projectionMatrix.Set(14, 2.0f * far * near / range);
            projectionMatrix.Set(11, -1.0f);
            projectionMatrix.Set(15, 1.0f);

            // Create the view matrix
            Vec3 zAxis = cameraPos - lookAt;
            zAxis = zAxis.Normalize();
            Vec3 xAxis = Vec3.cross(up, zAxis);
            xAxis = xAxis.Normalize();
            Vec3 yAxis = Vec3.cross(zAxis, xAxis);

            Mat4x4 viewMatrix = new Mat4x4();
            viewMatrix.Set(0, xAxis.x);
            viewMatrix.Set(4, xAxis.y);
            viewMatrix.Set(8, xAxis.z);
            viewMatrix.Set(12, -Vec3.dot(xAxis, cameraPos));
            viewMatrix.Set(1, yAxis.x);
            viewMatrix.Set(5, yAxis.y);
            viewMatrix.Set(9, yAxis.z);
            viewMatrix.Set(13, -Vec3.dot(yAxis, cameraPos));
            viewMatrix.Set(2, zAxis.x);
            viewMatrix.Set(6, zAxis.y);
            viewMatrix.Set(10, zAxis.z);
            viewMatrix.Set(14, -Vec3.dot(zAxis, cameraPos));
            viewMatrix.Set(15, 1.0f);

            vpMatrix = projectionMatrix * viewMatrix;

            return vpMatrix;
        }

        public static Mat4x4 CreateModelMatrix(Vec3 pos, Vec3 rotDegrees, Vec3 scale)
        {
            Mat4x4 modelMatrix = new Mat4x4();

            Mat4x4 scaleMatrix = new Mat4x4();
            scaleMatrix.Set(0, scale.x);
            scaleMatrix.Set(5, scale.y);
            scaleMatrix.Set(10, scale.z);
            scaleMatrix.Set(15, 1.0f);

            float rx = rotDegrees.x * (MathF.PI / 180.0f);
            float ry = rotDegrees.y * (MathF.PI / 180.0f);
            float rz = rotDegrees.z * (MathF.PI / 180.0f);

            Mat4x4 rotationX = new Mat4x4();
            rotationX.Set(0, 1.0f);
            rotationX.Set(5, MathF.Cos(rx));
            rotationX.Set(6, MathF.Sin(rx));
            rotationX.Set(9, -MathF.Sin(rx));
            rotationX.Set(10, MathF.Cos(rx));
            rotationX.Set(15, 1.0f);

            Mat4x4 rotationY = new Mat4x4();
            rotationY.Set(0, MathF.Cos(ry));
            rotationY.Set(2, -MathF.Sin(ry));
            rotationY.Set(5, 1.0f);
            rotationY.Set(8, MathF.Sin(ry));
            rotationY.Set(10, MathF.Cos(ry));
            rotationY.Set(15, 1.0f);

            Mat4x4 rotationZ = new Mat4x4();
            rotationZ.Set(0, MathF.Cos(rz));
            rotationZ.Set(1, MathF.Sin(rz));
            rotationZ.Set(4, -MathF.Sin(rz));
            rotationZ.Set(5, MathF.Cos(rz));
            rotationZ.Set(10, 1.0f);
            rotationZ.Set(15, 1.0f);

            Mat4x4 rotationMatrix = rotationX * rotationY * rotationZ;

            Mat4x4 translationMatrix = new Mat4x4();
            translationMatrix.Set(0, 1.0f);
            translationMatrix.Set(5, 1.0f);
            translationMatrix.Set(10, 1.0f);
            translationMatrix.Set(12, pos.x);
            translationMatrix.Set(13, pos.y);
            translationMatrix.Set(14, pos.z);
            translationMatrix.Set(15, 1.0f);

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
                        sum += mat1.Get(row + k * 4) * mat2.Get(k + col * 4);
                    }
                    result.Set(row + col * 4, sum);
                }
            }

            return result;
        }

    }

    public struct Vec2
    {
        public float x;
        public float y;

        public Vec2(float v)
        {
            this.x = v;
            this.y = v;
        }


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

        public static implicit operator Vec2(Vector2 v)
        {
            return new Vec2(v.X, v.Y);
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

        public static Vec2 Max(Vec2 a, Vec2 b)
        {
            return new Vec2(
                XMath.Max(a.x, b.x),
                XMath.Max(a.y, b.y)
            );
        }

        public static Vec2 Abs(Vec2 q)
        {
            return new Vec2(XMath.Abs(q.x), XMath.Abs(q.y));
        }

        public static Vec2 Normalize(Vec2 vec)
        {
            float length = vec.length();
            if (length == 0)
            {
                // Return a zero vector if the original vector is zero-length
                return new Vec2(0, 0);
            }
            return vec / length;
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
            return new Vec3(v1.x - v, v1.y - v, v1.z - v);
        }

        public static Vec3 operator -(float v, Vec3 v1)
        {
            return new Vec3(v - v1.x, v - v1.y, v - v1.z);
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
