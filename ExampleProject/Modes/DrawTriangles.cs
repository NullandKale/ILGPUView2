using GPU;
using System;
using ILGPU.Runtime;
using ILGPU;
using System.Collections.Generic;
using ILGPU.Algorithms;
using GPU.RT;
using Camera;

namespace ExampleProject.Modes
{
    public unsafe struct DrawTrianglesTiled : ITriangleImageFilterTiled
    {
        public int tick;
        public float aspectRatio;
        public float near;
        public float far;

        public Mat4x4 matrix;

        public Mat4x4 cameraMatrix;
        public Mat4x4 modelMatrix;

        public DrawTrianglesTiled(int tick, float hFovDegrees, int sizeX, int sizeY, float near, float far)
        {
            this.tick = tick;
            this.near = near;
            this.far = far;
            this.aspectRatio = (float)sizeX / (float)sizeY;

            float angle = ((tick / 3.0f) % 360.0f) * (MathF.PI / 180.0f);
            float camX = MathF.Sin(angle);
            float camZ = MathF.Cos(angle);
            Vec3 cameraPos = new Vec3(camX, -1f, camZ);
            Vec3 up = new Vec3(0, 1, 0);
            Vec3 lookAt = new Vec3(0, -0.35f, 0);

            cameraMatrix = Mat4x4.CreateCameraMatrix(cameraPos, up, lookAt, sizeX, sizeY, hFovDegrees, near, far);
            modelMatrix = Mat4x4.CreateModelMatrix(new Vec3(0, 0, 0), new Vec3(0, 0, 0), new Vec3(1, 1, 1));

            matrix = modelMatrix * cameraMatrix;
        }


        public void DrawTile(int tick, int xMin, int yMin, int xMax, int yMax, FrameBuffer output, ArrayView1D<Triangle, Stride1D.Dense> triangles)
        {
            for (int i = 0; i < triangles.Length; i++)
            {
                Triangle original = triangles[i];
                Vec4 v0 = matrix.MultiplyVector(new Vec4(original.v0.x, original.v0.y, original.v0.z, 1.0f));
                Vec4 v1 = matrix.MultiplyVector(new Vec4(original.v1.x, original.v1.y, original.v1.z, 1.0f));
                Vec4 v2 = matrix.MultiplyVector(new Vec4(original.v2.x, original.v2.y, original.v2.z, 1.0f));

                if (v0.w < 0 || v1.w < 0 || v2.w < 0)
                {
                    // triangle behind camera
                    continue;
                }

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
                    // back face culling
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
                                            (beta >= 0 && beta <= 1) &&
                                            (gamma >= 0 && gamma <= 1);

                        if (isInTriangle)
                        {
                            float depthValue = (alpha * t.v0.z + beta * t.v1.z + gamma * t.v2.z);
                            float normalizedDepth = 1.0f - ((depthValue - near) / (far - near));

                            if (normalizedDepth < output.GetDepth(x, y))
                            {
                                output.SetDepthPixel(x, y, normalizedDepth);

                                // this shows the mesh correctly
                                RGBA32 color = ComputeColorFromTriangle(x, y, t, (float)i / (float)triangles.Length);
                                
                                //RGBA32 color = new RGBA32(normalizedDepth, normalizedDepth, normalizedDepth);
                                

                                output.SetColorAt(x, y, color);
                            }
                        }
                    }
                }
            }
        }

        public RGBA32 GetColorClearColor()
        {
            return new RGBA32(0, 0, 0);
        }

        public float GetDepthClearColor()
        {
            return far;
        }

        private RGBA32 ComputeColorFromTriangle(float x, float y, Triangle triangle, float i)
        {
            float hue = i;
            float saturation = 1.0f;
            float lightness = 0.5f;

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
