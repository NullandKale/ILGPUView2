using static GPU.Kernels;
using GPU;
using UIElement;
using System.Windows.Input;
using ILGPUView2.GPU.DataStructures;
using System;
using ILGPU.Algorithms.Random;
using ILGPUView2.GPU.Filters;
using ILGPU.Algorithms;

namespace ExampleProject.Modes
{
    public class TexturedCube : IRenderCallback
    {
        private GPUMeshBatch meshes = new GPUMeshBatch();
        private GPUMegaTexture textures;
        private GPUFrameBuffer frameBuffer;

        private float fov = 75f;

        public void CreateUI()
        {
            UIBuilder.Clear();
            UIBuilder.AddLabel("Cube Renderer");

            var fovLabel = UIBuilder.AddLabel("");
            UIBuilder.AddSlider(fovLabel, "FOV: ", 1, 115, 75, (val) =>
            {
                fov = val;
            });
        }

        public void OnKeyPressed(Key key, ModifierKeys modifiers)
        {

        }

        public void OnLateRender(Renderer gpu)
        {

        }

        public void OnRender(Renderer gpu)
        {
            if (frameBuffer == null || frameBuffer.width != gpu.framebuffer.width || frameBuffer.height != gpu.framebuffer.height)
            {
                frameBuffer = new GPUFrameBuffer(gpu.framebuffer.width, gpu.framebuffer.height);
            }

            if (frameBuffer != null)
            {
                float angle = ((gpu.ticks / 5.0f) % 360.0f) * (MathF.PI / 180.0f);
                float radius = 1.0f;
                float camX = MathF.Sin(angle) * radius;
                float camZ = MathF.Cos(angle) * radius;
                Vec3 cameraPos = new Vec3(camX, 0, camZ);
                Vec3 up = new Vec3(0, 1, 0);
                Vec3 lookAt = new Vec3(0, 0, 0);

                gpu.ExecuteTriangleFilterMany(frameBuffer, meshes, textures,
                    new TextureShader(cameraPos, up, lookAt, frameBuffer.width, frameBuffer.height, fov, 0.01f, 1000, gpu.ticks));
                gpu.ExecuteFramebufferMask<FrameBufferCopy>(gpu.framebuffer, frameBuffer.toDevice(gpu));
            }
        }

        public void OnStart(Renderer gpu)
        {
            GPUMesh mesh = GPUMesh.CreateCube();
            //GPUMesh mesh = GPUMesh.LoadObjTriangles("Assets/cat.obj");

            mesh.SetPos(0, 0, 0);
            mesh.SetScale(0.5f, 0.5f, 0.5f);
            mesh.SetRot(0, 0, 0);

            meshes.AddMesh(mesh);

            textures = MegaTextureTest.loadTest();
        }

        public void OnStop()
        {

        }

        public void SetMode(int mode)
        {

        }

        public (int xSize, int ySize, bool update) BeforeResolutionChanged(RenderWindow renderWindow, int newWidth, int newHeight)
        {
            return (newWidth, newHeight, false);
        }
    }

    public unsafe struct TextureShader : ITriangleImageFilterTiled
    {
        public int tick;
        public float aspectRatio;
        public float near;
        public float far;

        public Mat4x4 cameraMatrix;

        public TextureShader(Vec3 cameraPos, Vec3 up, Vec3 lookAt, int sizeX, int sizeY, float hFovDegrees, float near, float far, int tick)
        {
            this.tick = tick;
            this.near = near;
            this.far = far;
            this.aspectRatio = (float)sizeX / (float)sizeY;

            cameraMatrix = Mat4x4.CreateCameraMatrix(cameraPos, up, lookAt, sizeX, sizeY, hFovDegrees, near, far);
        }

        public Mat4x4 GetCameraMat()
        {
            return cameraMatrix;
        }

        public RGBA32 GetColorClearColor()
        {
            // black
            return new RGBA32(0, 0, 0);
        }

        public float GetDepthClearColor()
        {
            return float.MinValue;
        }

        public float GetFar()
        {
            return far;
        }

        public float GetNear()
        {
            return near;
        }

        public RGBA32 FragShader(float x, float y, TransformedTriangle triangle, float i, dMegaTexture textures)
        {
            return new RGBA32(i, i, i);

            //return new RGBA32(XMath.Max(XMath.Min(x, 1), 0), 0, XMath.Max(XMath.Min(y, 1), 0));
            //return textures.GetColorAt(0, x, y);
        }

        public TransformedTriangle VertShader(Triangle original, Mat4x4 matrix, int width, int height)
        {
            Vec4 v0 = matrix.MultiplyVector(new Vec4(original.v0.x, original.v0.y, original.v0.z, 1.0f));
            Vec4 v1 = matrix.MultiplyVector(new Vec4(original.v1.x, original.v1.y, original.v1.z, 1.0f));
            Vec4 v2 = matrix.MultiplyVector(new Vec4(original.v2.x, original.v2.y, original.v2.z, 1.0f));

            return new TransformedTriangle(v0, v1, v2, original.uv0, original.uv1, original.uv2, width, height);
        }
    }

}
