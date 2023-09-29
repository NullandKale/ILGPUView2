using GPU;
using System;
using ILGPU.Runtime;
using ILGPU;
using System.Collections.Generic;
using ILGPU.Algorithms;
using GPU.RT;

namespace ExampleProject.Modes
{
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

        public static Mat4x4 CreateMVPMatrix(int tick, float aspectRatio, float near, float far)
        {
            Mat4x4 mat = new Mat4x4();

            // Setup projection matrix
            float fov = MathF.PI / 4.0f;  // 45 degrees
            float f = 1.0f / MathF.Tan(fov / 2.0f);
            float range = near - far;

            mat.Matrix[0] = f / aspectRatio;
            mat.Matrix[5] = f;
            mat.Matrix[10] = (far + near) / range;
            mat.Matrix[14] = 2.0f * far * near / range;
            mat.Matrix[11] = -1.0f;
            mat.Matrix[15] = 1.0f;

            // Calculate rotation angles based on tick
            float angleX = ((tick / 3.0f) % 360.0f) * (MathF.PI / 180.0f);
            float angleY = ((tick / 5.0f) % 360.0f) * (MathF.PI / 180.0f);

            // Rotation around the Y-axis
            Mat4x4 rotationY = new Mat4x4();
            rotationY.Matrix[0] = MathF.Cos(angleY);
            rotationY.Matrix[2] = MathF.Sin(angleY);
            rotationY.Matrix[8] = -MathF.Sin(angleY);
            rotationY.Matrix[10] = MathF.Cos(angleY);
            rotationY.Matrix[5] = 1.0f;
            rotationY.Matrix[15] = 1.0f;

            // Rotation around the X-axis
            Mat4x4 rotationX = new Mat4x4();
            rotationX.Matrix[5] = MathF.Cos(angleX);
            rotationX.Matrix[6] = MathF.Sin(angleX);
            rotationX.Matrix[9] = -MathF.Sin(angleX);
            rotationX.Matrix[10] = MathF.Cos(angleX);
            rotationX.Matrix[0] = 1.0f;
            rotationX.Matrix[15] = 1.0f;

            mat = mat * rotationY * rotationX;

            return mat;
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


    public unsafe struct DrawTrianglesTiled : ITriangleImageFilterMany
    {
        public int tick;
        public float aspectRatio;
        public float near;
        public float far;

        public Mat4x4 rotationMatrix;

        public DrawTrianglesTiled(int tick, float aspectRatio, float near, float far)
        {
            this.tick = tick;
            this.aspectRatio = aspectRatio;
            this.near = near;
            this.far = far;

            rotationMatrix = Mat4x4.CreateMVPMatrix(tick, aspectRatio, near, far);
        }

        public void DrawMany(int tick, int xMin, int yMin, int xMax, int yMax, dImage output, ArrayView2D<float, Stride2D.DenseY> depthBuffer, ArrayView1D<Triangle, Stride1D.Dense> triangles)
        {
            for (int i = 0; i < triangles.Length; i++)
            {
                // You can directly insert the replacement for MultiplyTriangle here
                Triangle original = triangles[i];
                Vec4 v0 = rotationMatrix.MultiplyVector(new Vec4(original.v0.x, original.v0.y, original.v0.z, 1.0f));
                Vec4 v1 = rotationMatrix.MultiplyVector(new Vec4(original.v1.x, original.v1.y, original.v1.z, 1.0f));
                Vec4 v2 = rotationMatrix.MultiplyVector(new Vec4(original.v2.x, original.v2.y, original.v2.z, 1.0f));

                Triangle t = new Triangle(
                    new Vec3(v0.x / v0.w, v0.y / v0.w, v0.z / v0.w),
                    new Vec3(v1.x / v1.w, v1.y / v1.w, v1.z / v1.w),
                    new Vec3(v2.x / v2.w, v2.y / v2.w, v2.z / v2.w)
                );

                Vec3 pv0 = new Vec3((t.v0.x + 1.0f) * output.width / 2f, (t.v0.y + 1.0f) * output.height / 2f, t.v0.z);
                Vec3 pv1 = new Vec3((t.v1.x + 1.0f) * output.width / 2f, (t.v1.y + 1.0f) * output.height / 2f, t.v1.z);
                Vec3 pv2 = new Vec3((t.v2.x + 1.0f) * output.width / 2f, (t.v2.y + 1.0f) * output.height / 2f, t.v2.z);

                float minX = XMath.Min(pv0.x, XMath.Min(pv1.x, pv2.x));
                float minY = XMath.Min(pv0.y, XMath.Min(pv1.y, pv2.y));
                float maxX = XMath.Max(pv0.x, XMath.Max(pv1.x, pv2.x));
                float maxY = XMath.Max(pv0.y, XMath.Max(pv1.y, pv2.y));

                minX = XMath.Max((int)XMath.Floor(minX), xMin);
                minY = XMath.Max((int)XMath.Floor(minY), yMin);
                maxX = XMath.Min((int)XMath.Ceiling(maxX), xMax);
                maxY = XMath.Min((int)XMath.Ceiling(maxY), yMax);

                float vec_x1 = t.v1.x - t.v0.x;
                float vec_y1 = t.v1.y - t.v0.y;
                float vec_x2 = t.v2.x - t.v0.x;
                float vec_y2 = t.v2.y - t.v0.y;

                float det = vec_x1 * vec_y2 - vec_x2 * vec_y1;

                if (det > 0)
                {
                    continue;
                }

                float invDet = 1.0f / det;

                for (int y = (int)minY; y <= (int)maxY; y++)
                {
                    for (int x = (int)minX; x <= (int)maxX; x++)
                    {
                        float fx = (float)x / output.width * 2.0f - 1.0f;
                        float fy = (float)y / output.height * 2.0f - 1.0f;

                        float vec_px = fx - t.v0.x;
                        float vec_py = fy - t.v0.y;

                        float alpha = (vec_px * vec_y2 - vec_x2 * vec_py) * invDet;
                        float beta = (vec_x1 * vec_py - vec_px * vec_y1) * invDet;
                        float gamma = 1.0f - alpha - beta;

                        bool isInTriangle = (alpha >= 0 && alpha <= 1) &&
                                            (beta  >= 0 && beta  <= 1) &&
                                            (gamma >= 0 && gamma <= 1);

                        if (isInTriangle)
                        {
                            int localX = x - xMin;
                            int localY = y - yMin;

                            float depth = alpha * t.v0.z + beta * t.v1.z + gamma * t.v2.z;
                            const float depthEpsilon = 0.1f;

                            if (depth < depthBuffer[localX, localY] + depthEpsilon)
                            {
                                depthBuffer[localX, localY] = depth;

                                RGBA32 color = ComputeColorFromTriangle(x, y, t, (float)i / (float)triangles.Length);

                                output.SetColorAt(x, y, color);
                            }
                        }
                    }
                }
            }
        }

        private RGBA32 ComputeColorFromTriangle(float x, float y, Triangle triangle, float i)
        {
            float hue = i;
            float saturation = 1.0f;  // fully saturated
            float lightness = 0.5f;   // moderate lightness

            // Convert HSL to RGB
            float r, g, b;
            if (saturation == 0)
            {
                r = g = b = lightness; // achromatic
            }
            else
            {
                float q = lightness < 0.5 ? lightness * (1 + saturation) : lightness + saturation - lightness * saturation;
                float p = 2 * lightness - q;
                r = HueToRGB(p, q, hue + 1 / 3.0f);
                g = HueToRGB(p, q, hue);
                b = HueToRGB(p, q, hue - 1 / 3.0f);
            }

            // Convert RGB from [0,1] to [0,255]
            return new RGBA32((byte)(r * 255), (byte)(g * 255), (byte)(b * 255), 255);
        }

        private float HueToRGB(float p, float q, float t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1 / 6.0) return p + (q - p) * 6 * t;
            if (t < 1 / 2.0) return q;
            if (t < 2 / 3.0) return p + (q - p) * (2 / 3.0f - t) * 6;
            return p;
        }
    }

}
