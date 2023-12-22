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
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace ExampleProject.Modes
{
    public class MeshRenderer : IRenderCallback
    {
        private GLTFLoader loader;
        private GPUMeshBatch meshes;
        private GPUMegaTexture textures;
        private GPUFrameBuffer frameBuffer;
        private float fov = 75;

        private Label stats;
        private Label totalTime;
        private Label clearTime;
        private Label transformTime;
        private Label cacheFillTime;
        private Label drawTime;

        public void CreateUI()
        {
            UIBuilder.Clear();
            stats = UIBuilder.AddLabel("Rasterization Stats:");
            totalTime = UIBuilder.AddLabel("Total time: ");
            clearTime = UIBuilder.AddLabel("Clear time: ");
            transformTime = UIBuilder.AddLabel("Transform time: ");
            cacheFillTime = UIBuilder.AddLabel("Tile Cache Fill time: ");
            drawTime = UIBuilder.AddLabel("Draw time: ");
            UIBuilder.AddButton("Time Rendering", () =>
            {
                Renderer.timeEachStep = !Renderer.timeEachStep;
            });

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

            textures = MegaTextureTest.loadTest();
        }

        public void OnKeyPressed(Key key, ModifierKeys modifiers)
        {

        }

        public void OnLateRender(Renderer gpu)
        {
            var data = gpu.CalculateRasterizationKernelTimings();
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if(Renderer.timeEachStep)
                {
                    totalTime.Content = "Total time: " + data.averageTotalTime.ToString("F3") + " ms";
                    clearTime.Content = "Clear time: " + data.averageClearTime.ToString("F3") + " ms";
                    transformTime.Content = "Transform time: " + data.averageTransformTime.ToString("F3") + " ms";
                    cacheFillTime.Content = "Tile Cache Fill time: " + data.averageFillTileCacheTime.ToString("F3") + " ms";
                    drawTime.Content = "Draw time: " + data.averageDrawTime.ToString("F3") + " ms";
                }
                else
                {
                    totalTime.Content = "";
                    clearTime.Content = "";
                    transformTime.Content = "";
                    cacheFillTime.Content = "";
                    drawTime.Content = "";
                }
            });
        }

        public void OnRender(Renderer gpu)
        {
            if(frameBuffer == null || frameBuffer.width != gpu.framebuffer.width || frameBuffer.height != gpu.framebuffer.height)
            {
                frameBuffer = new GPUFrameBuffer(gpu.framebuffer.width, gpu.framebuffer.height);
            }

            if(frameBuffer != null)
            {
                float angle = ((gpu.ticks / 5.0f) % 360.0f) * (MathF.PI / 180.0f);
                float radius = 1.25f;
                float camX = MathF.Sin(angle) * radius;
                float camZ = MathF.Cos(angle) * radius;
                Vec3 cameraPos = new Vec3(camX, 0, camZ);
                Vec3 up = new Vec3(0, 1, 0);
                Vec3 lookAt = new Vec3(0, 0, 0);

                gpu.ExecuteTriangleFilterMany(frameBuffer, meshes, textures,
                    new DrawTrianglesTiled(cameraPos, up, lookAt, frameBuffer.width, frameBuffer.height, fov, 0.01f, 1000, gpu.ticks));

                gpu.ExecuteFramebufferMask<FrameBufferCopy>(gpu.framebuffer, frameBuffer.toDevice(gpu));
            }
        }

        public void OnStart(Renderer gpu)
        {
            meshes = new GPUMeshBatch();

            float spacing = 0.6f;
            float scaleRatio = 0.25f;
            //int triangleMinimum = 4000;
            //int triangleMinimum = 500000;
            int triangleMinimum = 750000;

            AddCatsInCylinder(triangleMinimum, new Vec3(0, 0.1, 0), spacing, scaleRatio);

            Trace.WriteLine("Actually Loaded Triangles: " + meshes.triangleCount);

            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                stats.Content = "Rasterizing " + meshes.triangleCount + " triangles:";
            });
        }

        public void AddCatsInCylinder(int triangleLimit, Vec3 start, float spacing, float scaleRatio)
        {
            int layer = 0;
            bool reachedLimit = false;
            Vec3 scale = new Vec3(spacing * scaleRatio, spacing * scaleRatio, spacing * scaleRatio);
            Random rng = new Random(0);
            
            GPUMesh cat = GPUMesh.LoadObjTriangles("Assets/cat.obj");
            //GPUMesh cat = GPUMesh.CreateCube();

            while (true)
            {
                bool addedInThisLayer = false;

                int catsInRing = layer > 0 ? (int)(2.0 * Math.PI * layer / spacing) : 1; // Calculate number of cats in the current ring

                for (int j = -layer; j <= layer; j++) // Iterate over height
                {
                    for (int i = 0; i < catsInRing; i++) // Iterate over circumference
                    {
                        float angle = i * 2.0f * (float)Math.PI / catsInRing;
                        float radius = layer * spacing;
                        float x = start.x + radius * MathF.Cos(angle);
                        float y = start.y + j * spacing * 0.60f; // Adjust the y to fit the aspect ratio
                        float z = start.z + radius * MathF.Sin(angle);

                        cat.SetPos(x, y, z);
                        cat.SetScale(scale.x, scale.y, scale.z);
                        cat.SetRot((float)rng.NextDouble() * 360.0f, (float)rng.NextDouble() * 360.0f, (float)rng.NextDouble() * 360.0f);
            
                        meshes.AddMesh(cat);

                        addedInThisLayer = true;

                        if (meshes.triangleCount >= triangleLimit)
                        {
                            reachedLimit = true;
                        }
                    }
                }

                if (!addedInThisLayer || reachedLimit)
                {
                    break; // Stop if no cats were added in this layer or if the limit has been reached
                }

                layer++; // Increase the layer for the next iteration
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
