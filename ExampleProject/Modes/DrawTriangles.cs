using GPU;
using System;
using ILGPU.Runtime;
using ILGPU;
using System.Collections.Generic;
using ILGPU.Algorithms;
using GPU.RT;
using Camera;
using ILGPU.Runtime.Cuda;

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
            return float.MaxValue;
        }

        public float GetFar()
        {
            return far;
        }

        public float GetNear()
        {
            return near;
        }

        public RGBA32 FragShader(float x, float y, TransformedTriangle triangle, float i)
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

        public TransformedTriangle VertShader(Triangle original, dMesh mesh, int width, int height)
        {
            Vec4 v0 = mesh.matrix.MultiplyVector(new Vec4(original.v0.x, original.v0.y, original.v0.z, 1.0f));
            Vec4 v1 = mesh.matrix.MultiplyVector(new Vec4(original.v1.x, original.v1.y, original.v1.z, 1.0f));
            Vec4 v2 = mesh.matrix.MultiplyVector(new Vec4(original.v2.x, original.v2.y, original.v2.z, 1.0f));

            return new TransformedTriangle(v0, v1, v2, width, height);
        }
    }

}