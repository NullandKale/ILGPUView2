using static GPU.Kernels;
using GPU;
using UIElement;
using System.Windows.Input;
using ILGPU.Algorithms;
using ILGPUView2.GPU.DataStructures;
using System;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Numerics;

namespace ExampleProject.Modes
{
    public struct Line
    {
        public Vec3 start;
        public Vec3 end;

        public Line(Vec3 start, Vec3 end)
        {
            this.start = start;
            this.end = end;
        }

        public Line(Camera3D camera, Random rng)
        {

        }
    }

    public struct LineMask : IKernelMask<Line>
    {
        public Camera3D camera;

        public LineMask(Camera3D camera)
        {
            this.camera = camera;
        }

        // not finished
        // this is called for every line in dBuffer
        // index is the index of the line in dBuffer
        // draw the line to the fraembuffer
        public void Apply(int tick, int index, dImage framebuffer, dBuffer<Line> dBuffer)
        {
            //// Get the line from the dBuffer at the specified index
            //Line line = dBuffer.Get(index);

            //// Project the start and end points of the line from world space to screen space using the camera's projection matrix
            //Vector3 startScreenPos = camera.projectionMatrix * camera.viewMatrix * line.start;
            //Vector3 endScreenPos = camera.projectionMatrix * camera.viewMatrix * line.end;

            //// Convert the projected positions from homogeneous coordinates to screen coordinates
            //Vector2 startScreenCoords = new Vector2(startScreenPos.x / startScreenPos.w, startScreenPos.y / startScreenPos.w);
            //Vector2 endScreenCoords = new Vector2(endScreenPos.x / endScreenPos.w, endScreenPos.y / endScreenPos.w);

            //// Draw the line to the framebuffer using the screen coordinates
            //framebuffer.DrawLine(startScreenCoords, endScreenCoords, Color.white);
        }
    }



    public class BufferTest : IRenderCallback
    {
        GPUBuffer<Line> linebuffer;

        public void CreateUI()
        {
            UIBuilder.Clear();
            UIBuilder.AddLabel("BufferTest");
            UIBuilder.AddLabel("Not Finished");
        }

        public void OnKeyPressed(Key key, ModifierKeys modifiers)
        {

        }

        public void OnRender(Device gpu)
        {
            
        }

        public void OnStart(Device gpu)
        {
            linebuffer = new GPUBuffer<Line>(gpu.device, 100);
        }

        public void OnStop()
        {

        }

        public void SetMode(int mode)
        {

        }
    }

}
