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
        private MemoryBuffer1D<Triangle, Stride1D.Dense> triangles;
        private GPUFrameBuffer frameBuffer;

        public void CreateUI()
        {
            UIBuilder.Clear();
            UIBuilder.AddLabel("Debug Renderer");
        }

        public void OnKeyPressed(Key key, ModifierKeys modifiers)
        {

        }

        public void OnLateRender(GPU.Device gpu)
        {

        }

        public void OnRender(GPU.Device gpu)
        {
            if(frameBuffer == null || frameBuffer.width == gpu.framebuffer.width || frameBuffer.height == gpu.framebuffer.height)
            {
                frameBuffer = new GPUFrameBuffer(gpu.framebuffer.width, gpu.framebuffer.height);
            }

            if(frameBuffer != null)
            {
                gpu.ExecuteTriangleFilterMany(frameBuffer, triangles, new DrawTrianglesTiled(gpu.ticks, 75, frameBuffer.width, frameBuffer.height, 0.1f, 1000));
                gpu.ExecuteFramebufferMask<FrameBufferCopy>(gpu.framebuffer, frameBuffer.toDevice(gpu));
            }
        }

        public void OnStart(GPU.Device gpu)
        {
            //Triangle[] loadedTriangles = CreateCubeTriangles();
            Triangle[] loadedTriangles = LoadObjTriangles("Assets/cat.obj");

            triangles = gpu.device.Allocate1D(loadedTriangles);
        }

        private Triangle[] LoadObjTriangles(string filename)
        {
            List<Vec3> vertices = new List<Vec3>();
            List<Triangle> triangles = new List<Triangle>();

            string[] lines = File.ReadAllLines(filename);

            foreach (string line in lines)
            {
                string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;

                switch (parts[0])
                {
                    case "v":
                        vertices.Add(new Vec3(
                            -float.Parse(parts[1]),
                            -float.Parse(parts[2]),
                            -float.Parse(parts[3])
                        ));
                        break;
                    case "f":
                        var indices = parts.Skip(1).Select(p => int.Parse(p.Split('/')[0]) - 1).ToArray();
                        for (int i = 1; i < indices.Length - 1; ++i)
                        {
                            triangles.Add(new Triangle(
                                vertices[indices[0]],
                                vertices[indices[i]],
                                vertices[indices[i + 1]]
                            ));
                        }
                        break;
                }
            }

            return triangles.ToArray();
        }


        private Triangle[] CreateCubeTriangles()
        {
            float scale = 0.5f;
            // Define the 8 vertices of the cube
            Vec3[] vertices = new Vec3[]
            {
                new Vec3(-scale, -scale, -scale),  // 0
                new Vec3( scale, -scale, -scale),  // 1
                new Vec3( scale,  scale, -scale),  // 2
                new Vec3(-scale,  scale, -scale),  // 3
                new Vec3(-scale, -scale,  scale),  // 4
                new Vec3( scale, -scale,  scale),  // 5
                new Vec3( scale,  scale,  scale),  // 6
                new Vec3(-scale,  scale,  scale)   // 7
            };

            // Define the 12 triangles using the vertices with counter-clockwise winding
            Triangle[] triangles = new Triangle[]
            {
                // Front face
                new Triangle(vertices[0], vertices[2], vertices[1]),
                new Triangle(vertices[2], vertices[0], vertices[3]),
                // Back face
                new Triangle(vertices[5], vertices[4], vertices[7]),
                new Triangle(vertices[7], vertices[6], vertices[5]),
                // Left face
                new Triangle(vertices[3], vertices[7], vertices[0]),
                new Triangle(vertices[4], vertices[0], vertices[7]),
                // Right face
                new Triangle(vertices[2], vertices[1], vertices[5]),
                new Triangle(vertices[5], vertices[6], vertices[2]),
                // Top face
                new Triangle(vertices[3], vertices[2], vertices[6]),
                new Triangle(vertices[6], vertices[7], vertices[3]),
                // Bottom face
                new Triangle(vertices[1], vertices[0], vertices[4]),
                new Triangle(vertices[4], vertices[5], vertices[1])
            };


            return triangles;
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
