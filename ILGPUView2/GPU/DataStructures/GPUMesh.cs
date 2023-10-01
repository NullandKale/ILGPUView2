using GPU;
using ILGPU.Runtime;
using ILGPU;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace GPU
{
    public class GPUMesh
    {
        private Vec3 pos;
        private Vec3 rotDegrees;
        private Vec3 scale;

        private Triangle[] trianglesCPU;
        private MemoryBuffer1D<Triangle, Stride1D.Dense> triangles;
        private MemoryBuffer1D<TransformedTriangle, Stride1D.Dense> workingTriangles;

        private bool cpuDirty = false;
        private bool gpuDirty = false;

        public GPUMesh(List<Triangle> triangles)
        {
            this.trianglesCPU = triangles.ToArray();
            pos = new Vec3(0, 0, 0);
            rotDegrees = new Vec3(0, 0, 0);
            scale = new Vec3(1, 1, 1);
        }

        public static GPUMesh CreateCube()
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

            return new GPUMesh(new List<Triangle>(triangles));
        }

        public static GPUMesh LoadObjTriangles(string filename)
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

            return new GPUMesh(triangles);
        }

        public dMesh toGPU(GPU.Renderer gpu)
        {
            if (triangles == null || triangles.Extent != trianglesCPU.Length)
            {
                if (trianglesCPU != null && trianglesCPU.Length > 0)
                {
                    triangles = gpu.device.Allocate1D(trianglesCPU);
                }
                else
                {
                    triangles = gpu.device.Allocate1D<Triangle>(trianglesCPU.Length);
                }
            }

            if (cpuDirty)
            {
                triangles.CopyFromCPU(trianglesCPU);
                cpuDirty = false;
            }

            if (workingTriangles == null)
            {
                workingTriangles = gpu.device.Allocate1D<TransformedTriangle>(trianglesCPU.Length);
            }

            gpuDirty = true;

            return new dMesh(pos, rotDegrees, scale, triangles, workingTriangles);
        }

        public void toCPU()
        {
            if (triangles != null && workingTriangles != null)
            {
                if (trianglesCPU == null || trianglesCPU.Length != triangles.Length)
                {
                    trianglesCPU = new Triangle[triangles.Length];
                    gpuDirty = true;
                }

                if (gpuDirty)
                {
                    triangles.CopyToCPU(trianglesCPU);
                    gpuDirty = false;
                }
            }
        }
    }


    public struct dMesh
    {
        public Mat4x4 matrix;
        public Mat4x4 modelMatrix;
        public ArrayView1D<Triangle, Stride1D.Dense> triangles;
        public ArrayView1D<TransformedTriangle, Stride1D.Dense> workingTriangles;

        public dMesh(Vec3 pos, Vec3 rotDegrees, Vec3 scale, ArrayView1D<Triangle, Stride1D.Dense> triangles, ArrayView1D<TransformedTriangle, Stride1D.Dense> workingTriangles)
        {
            this.modelMatrix = Mat4x4.CreateModelMatrix(pos, rotDegrees, scale);

            this.triangles = triangles;
            this.workingTriangles = workingTriangles;
        }

        public void ApplyCamera(Mat4x4 cameraMatrix)
        {
            matrix = modelMatrix * cameraMatrix;
        }
    }
}
