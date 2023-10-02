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

namespace ExampleProject.Modes
{
    public class MeshRenderer : IRenderCallback
    {
        private List<GPUMesh> meshes;
        private GPUFrameBuffer frameBuffer;

        public void CreateUI()
        {
            UIBuilder.Clear();
            UIBuilder.AddLabel("Debug Renderer");
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
                gpu.ExecuteTriangleFilterMany(frameBuffer, meshes, new DrawTrianglesTiled(gpu.ticks, 75, frameBuffer.width, frameBuffer.height, 0.1f, 1000));
                gpu.ExecuteFramebufferMask<FrameBufferCopy>(gpu.framebuffer, frameBuffer.toDevice(gpu));
            }
        }

        public void OnStart(GPU.Renderer gpu)
        {
            meshes = new List<GPUMesh>();

            GPUMesh cat0 = GPUMesh.LoadObjTriangles("Assets/cat.obj");

            GPUMesh cat1 = GPUMesh.LoadObjTriangles("Assets/cat.obj");
            cat1.SetPos(0, 0, 2);

            GPUMesh cube = GPUMesh.CreateCube();
            cube.SetPos(0, 1.5f, 0);
            //cube.SetScale(5, 1, 5);

            meshes.Add(cat0);
            meshes.Add(cat1);
            meshes.Add(cube);
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
