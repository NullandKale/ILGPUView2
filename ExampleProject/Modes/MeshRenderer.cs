using GPU;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static GPU.Kernels;
using System.Windows.Input;
using UIElement;
using ExampleProject.Modes;
using ILGPUView2.GPU.DataStructures;
using ILGPU.Runtime;
using ILGPU;
using ILGPU.Algorithms;
using System.Reflection.Metadata;
using System;
using System.IO;
using Camera;
using System.Numerics;

namespace ExampleProject.Modes
{
    public class MeshRenderer : IRenderCallback
    {
        private GLTFLoader loader;
        private GPUMeshBatch meshes;
        private GPUFrameBuffer frameBuffer;
        private float fov = 75;

        bool initialized;

        public void CreateUI()
        {
            UIBuilder.Clear();
            UIBuilder.AddLabel("Debug Renderer");

            string rootDirectory = @"..\..\..\Assets\glTF-Sample-Models\2.0\";

            if(Directory.Exists(rootDirectory))
            {
                GLTFLoader loader = new GLTFLoader(rootDirectory);
                List<string> gltfs = loader.ListAvailableGLTFs();
                List<string> names = loader.ListAvailableGLTFNames();

                UIBuilder.AddLabel("This might crash your computer, some of these are untested, and most gltf features are unsupported");
                UIBuilder.AddDropdown(names.ToArray(), (selected) =>
                {
                    try
                    {
                        var newMeshes = loader.LoadGLTF(gltfs[selected]);
                        if (newMeshes != null && newMeshes.triangleCount > 0)
                        {
                            meshes = newMeshes;
                        }
                    }
                    catch
                    {

                    }

                });
            }

            var fovLabel = UIBuilder.AddLabel("");
            UIBuilder.AddSlider(fovLabel, "FOV: ", 1, 115, 75, (val) =>
            {
                fov = val;
            });
        }

        public void OnKeyPressed(Key key, ModifierKeys modifiers)
        {

        }

        public void OnLateRender(GPU.Renderer gpu)
        {

        }

        public void OnRender(GPU.Renderer gpu)
        {
            if(frameBuffer == null || frameBuffer.width != gpu.framebuffer.width || frameBuffer.height != gpu.framebuffer.height)
            {
                frameBuffer = new GPUFrameBuffer(gpu.framebuffer.width, gpu.framebuffer.height);
            }

            if(frameBuffer != null)
            {
                float angle = ((gpu.ticks / 3.0f) % 360.0f) * (MathF.PI / 180.0f);
                float camX = MathF.Sin(angle);
                float camZ = MathF.Cos(angle);
                Vec3 cameraPos = new Vec3(camX, 0, camZ);
                Vec3 up = new Vec3(0, 1, 0);
                Vec3 lookAt = new Vec3(0, 0, 0);


                gpu.ExecuteTriangleFilterMany(frameBuffer, meshes, new DrawTrianglesTiled(cameraPos, up, lookAt, frameBuffer.width, frameBuffer.height, fov, 0.1f, 1000, gpu.ticks));
                gpu.ExecuteFramebufferMask<FrameBufferCopy>(gpu.framebuffer, frameBuffer.toDevice(gpu));
            }
        }

        public void OnStart(GPU.Renderer gpu)
        {
            meshes = new GPUMeshBatch();

            AddConcentricCirclesOfCats(1, new Vec3(), 0, new Vec3(1, 1, 1));
            AddConcentricCirclesOfCats(25, new Vec3(), 1.5f, new Vec3(0.3, 0.3, 0.3));
            //AddConcentricCirclesOfCats(50, new Vec3(), 2f, new Vec3(0.3, 0.3, 0.3));
            //AddConcentricCirclesOfCats(100, new Vec3(), 2.5f, new Vec3(0.1, 0.1, 0.1));
            //AddConcentricCirclesOfCats(100, new Vec3(), 3f, new Vec3(0.1, 0.1, 0.1));
            //AddConcentricCirclesOfCats(150, new Vec3(), 3.5f, new Vec3(0.1, 0.1, 0.1));
            //AddConcentricCirclesOfCats(150, new Vec3(), 4f, new Vec3(0.1, 0.1, 0.1));
        }

        public void AddGridOfCubes(int count, Vec3 centerPos, float minDistBetweenObjects, Vec3 scale)
        {
            int sideCount = (int)Math.Sqrt(count);
            float offset = minDistBetweenObjects;
            float halfGrid = (sideCount - 1) * offset / 2.0f;  // Half the size of the grid

            for (int i = 0; i < sideCount; i++)
            {
                for (int j = 0; j < sideCount; j++)
                {
                    GPUMesh cube = GPUMesh.CreateCube();
                    Vec3 position = new Vec3(centerPos.x + i * offset - halfGrid, centerPos.y, centerPos.z + j * offset - halfGrid);
                    cube.SetPos(position.x, position.y, position.z);
                    cube.SetScale(scale.x, scale.y, scale.z);
                    meshes.AddMesh(cube);
                }
            }
        }

        public void AddConcentricCirclesOfCats(int count, Vec3 centerPos, float minDistBetweenObjects, Vec3 scale)
        {
            float angleIncrement = 360.0f / count;

            for (int i = 1; i <= count; i++)
            {
                float angle = i * angleIncrement;
                float x = centerPos.x + minDistBetweenObjects * (float)Math.Cos(Math.PI * angle / 180.0);
                float z = centerPos.z + minDistBetweenObjects * (float)Math.Sin(Math.PI * angle / 180.0);

                GPUMesh cat = GPUMesh.LoadObjTriangles("Assets/cat.obj");
                cat.SetPos(x, centerPos.y + (scale.y * 2), z);
                cat.SetScale(scale.x, scale.y, scale.z);

                // Rotate them to look at zero, they face Vec3(1, 0, 0);
                // Assuming that rotation is done in Euler angles, and the initial facing direction is Vec3(1, 0, 0)
                float yRotDegrees = -angle;
                cat.SetRot(0, yRotDegrees, 0);

                meshes.AddMesh(cat);
            }
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

}
