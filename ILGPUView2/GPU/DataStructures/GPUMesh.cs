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
        public int TriangleCount;

        // store data for model matrix
        // we will need lists of these for the GPUMeshBatch to store the model transform
        public Vec3 pos;
        public Vec3 rotDegrees;
        public Vec3 scale;

        public Triangle[] trianglesCPU;
        public MemoryBuffer1D<Triangle, Stride1D.Dense> triangles;
        public MemoryBuffer1D<TransformedTriangle, Stride1D.Dense> workingTriangles;

        public bool cpuDirty = false;
        public bool gpuDirty = false;

        public GPUMesh(List<Triangle> triangles)
        {
            this.trianglesCPU = triangles.ToArray();
            pos = new Vec3(0, 0, 0);
            rotDegrees = new Vec3(0, 0, 0);
            scale = new Vec3(1, 1, 1);
            this.TriangleCount = triangles.Count;
        }

        public static GPUMesh CreateCube()
        {
            float scale = 1.0f;
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

            // UV coordinates for each vertex
            Vec2[] uvs = new Vec2[]
            {
                new Vec2(0.0f, 0.0f),  // Bottom-left
                new Vec2(1.0f, 0.0f),  // Bottom-right
                new Vec2(1.0f, 1.0f),  // Top-right
                new Vec2(0.0f, 1.0f)   // Top-left
            };

            // Define the triangles of the cube
            int[][] indices = new int[][]
            {
                // Front face
                new int[] { 0, 1, 2, 2, 3, 0 },
                // Back face
                new int[] { 5, 4, 7, 7, 6, 5 },
                // Left face
                new int[] { 3, 7, 4, 4, 0, 3 },
                // Right face
                new int[] { 1, 5, 6, 6, 2, 1 },
                // Top face
                new int[] { 3, 2, 6, 6, 7, 3 },
                // Bottom face
                new int[] { 4, 5, 1, 1, 0, 4 }
            };

            List<Triangle> triangles = new List<Triangle>();
            foreach (int[] face in indices)
            {
                for (int i = 0; i < face.Length; i += 3)
                {
                    Vec3 v0 = vertices[face[i]];
                    Vec3 v1 = vertices[face[i + 1]];
                    Vec3 v2 = vertices[face[i + 2]];

                    Vec2 uv0 = uvs[face[i] % 4];
                    Vec2 uv1 = uvs[face[i + 1] % 4];
                    Vec2 uv2 = uvs[face[i + 2] % 4];

                    triangles.Add(new Triangle(v0, v1, v2, uv0, uv1, uv2));
                }
            }

            return new GPUMesh(triangles);
        }


        public static GPUMesh LoadObjTriangles(string filename)
        {
            List<Vec3> vertices = new List<Vec3>();
            List<Vec2> uvs = new List<Vec2>(); // Store UVs
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
                    case "vt":
                        uvs.Add(new Vec2(
                            float.Parse(parts[1]),
                            float.Parse(parts[2])
                        ));
                        break;
                    case "f":
                        var vertexIndices = parts.Skip(1).Select(p => int.Parse(p.Split('/')[0]) - 1).ToArray();
                        var uvIndices = parts.Skip(1).Select(p => p.Contains('/') ? int.Parse(p.Split('/')[1]) - 1 : -1).ToArray();
                        for (int i = 1; i < vertexIndices.Length - 1; ++i)
                        {
                            triangles.Add(new Triangle(
                                vertices[vertexIndices[0]],
                                vertices[vertexIndices[i]],
                                vertices[vertexIndices[i + 1]],
                                uvIndices[0] >= 0 ? uvs[uvIndices[0]] : new Vec2(0, 0),
                                uvIndices[i] >= 0 ? uvs[uvIndices[i]] : new Vec2(0, 0),
                                uvIndices[i + 1] >= 0 ? uvs[uvIndices[i + 1]] : new Vec2(0, 0)
                            ));
                        }
                        break;
                }
            }

            return new GPUMesh(triangles);
        }


        // model position
        public void SetPos(float x, float y, float z)
        {
            this.pos = new Vec3(x, y, z);

        }

        // model rotate
        public void SetRot(float xRotDegrees, float yRotDegrees, float zRotDegrees)
        {
            this.rotDegrees = new Vec3(xRotDegrees, yRotDegrees, zRotDegrees);
        }

        // model scale
        public void SetScale(float x, float y, float z)
        {
            this.scale = new Vec3(x, y, z);
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
            // create the model matrix
            this.modelMatrix = Mat4x4.CreateModelMatrix(pos, rotDegrees, scale);

            this.triangles = triangles;
            this.workingTriangles = workingTriangles;
        }

        // we will need a batch version of this in GPUMeshBatch NOT in dMeshBatch or dMeshTicket because we store these in a memorybuffer / arrayview
        public void ApplyCamera(Mat4x4 cameraMatrix)
        {
            // apply a camera matrix
            this.matrix = cameraMatrix * modelMatrix;
        }
    }
}
