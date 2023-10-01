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
        private GPUMesh mesh;
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
                gpu.ExecuteTriangleFilterMany(frameBuffer, mesh, new DrawTrianglesTiled(gpu.ticks, 75, frameBuffer.width, frameBuffer.height, 0.1f, 1000));
                gpu.ExecuteFramebufferMask<FrameBufferCopy>(gpu.framebuffer, frameBuffer.toDevice(gpu));
            }
        }

        public void OnStart(GPU.Renderer gpu)
        {
            mesh = GPUMesh.LoadObjTriangles("Assets/cat.obj");
            //mesh = GPUMesh.CreateCube();
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
