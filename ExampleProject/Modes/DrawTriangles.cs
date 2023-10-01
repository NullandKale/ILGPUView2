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

        public Mat4x4 cameraMatrix;

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
        }

        public void DrawTile(int tick, int xMin, int yMin, int xMax, int yMax, FrameBuffer output, dMesh mesh)
        {
            // needs to be really high for skinny triangles
            float epsilon = ITriangleImageFilterTiled.tileSize * 200.0f;

            for (int i = 0; i < mesh.triangles.Length; i++)
            {
                // Early exit if the triangle is completely outside the tile
                if (mesh.workingTriangles[i].maxX < xMin - epsilon || mesh.workingTriangles[i].minX > xMax + epsilon || mesh.workingTriangles[i].maxY < yMin - epsilon || mesh.workingTriangles[i].minY > yMax + epsilon)
                {
                    continue;
                }

                int minX = XMath.Max((int)XMath.Floor(mesh.workingTriangles[i].minX), xMin);
                int minY = XMath.Max((int)XMath.Floor(mesh.workingTriangles[i].minY), yMin);
                int maxX = XMath.Min((int)XMath.Ceiling(mesh.workingTriangles[i].maxX), xMax);
                int maxY = XMath.Min((int)XMath.Ceiling(mesh.workingTriangles[i].maxY), yMax);

                Vec3 v0 = mesh.workingTriangles[i].v0;
                Vec3 v1 = mesh.workingTriangles[i].v1;
                Vec3 v2 = mesh.workingTriangles[i].v2;

                float vec_x1 = v1.x - v0.x;
                float vec_y1 = v1.y - v0.y;
                float vec_x2 = v2.x - v0.x;
                float vec_y2 = v2.y - v0.y;

                float det = vec_x1 * vec_y2 - vec_x2 * vec_y1;

                if (det > 0)
                {
                    continue;
                }

                float invDet = 1.0f / det;

                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        float fx = (float)x / output.width * 2.0f - 1.0f;
                        float fy = (float)y / output.height * 2.0f - 1.0f;

                        float vec_px = fx - v0.x;
                        float vec_py = fy - v0.y;

                        float alpha = (vec_px * vec_y2 - vec_x2 * vec_py) * invDet;
                        float beta = (vec_x1 * vec_py - vec_px * vec_y1) * invDet;
                        float gamma = 1.0f - alpha - beta;

                        bool isInTriangle = (alpha >= 0 && alpha <= 1) &&
                                            (beta >= 0 && beta <= 1) &&
                                            (gamma >= 0 && gamma <= 1);

                        if (isInTriangle)
                        {
                            float depthValue = (alpha * v0.z + beta * v1.z + gamma * v2.z);
                            float normalizedDepth = 1.0f - ((depthValue - near) / (far - near));

                            if (normalizedDepth < output.GetDepth(x, y))
                            {
                                output.SetDepthPixel(x, y, normalizedDepth);

                                RGBA32 color = ComputeColorFromTriangle(x, y, mesh.workingTriangles[i], (float)i / (float)mesh.triangles.Length);
                                //RGBA32 color = new RGBA32(normalizedDepth, normalizedDepth, normalizedDepth);

                                output.SetColorAt(x, y, color);
                            }
                        }
                    }
                }
            }
        }

        public Mat4x4 GetCameraMat()
        {
            return cameraMatrix;
        }

        public RGBA32 GetColorClearColor()
        {
            return new RGBA32(0, 0, 0);
        }

        public float GetDepthClearColor()
        {
            return far;
        }

        private RGBA32 ComputeColorFromTriangle(float x, float y, TransformedTriangle triangle, float i)
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
