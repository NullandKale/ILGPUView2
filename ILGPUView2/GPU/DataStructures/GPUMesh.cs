using GPU;
using ILGPU.Runtime;
using ILGPU;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using SharpGLTF.Schema2;

namespace GPU
{
    public class GLTFLoader
    {
        private readonly string rootDirectory;

        public GLTFLoader(string rootDirectory)
        {
            this.rootDirectory = rootDirectory;
        }

        public List<string> ListAvailableGLTFs()
        {
            List<string> availableFiles = new List<string>();

            // Get all subdirectories under the root directory
            string[] subdirectories = Directory.GetDirectories(rootDirectory);

            foreach (string subdir in subdirectories)
            {
                // Check if the subdirectory contains a glTF folder
                string gltfFolder = Path.Combine(subdir, "glTF");
                if (Directory.Exists(gltfFolder))
                {
                    // List all glTF files within the glTF folder
                    string[] gltfFiles = Directory.GetFiles(gltfFolder, "*.gltf");
                    availableFiles.AddRange(gltfFiles);
                }
            }

            return availableFiles;
        }

        public List<string> ListAvailableGLTFNames()
        {
            List<string> availableNames = new List<string>();

            string[] subdirectories = Directory.GetDirectories(rootDirectory);
            foreach (string subdir in subdirectories)
            {
                string folderName = new DirectoryInfo(subdir).Name;
                string gltfFolder = Path.Combine(subdir, "glTF");
                if (Directory.Exists(gltfFolder))
                {
                    availableNames.Add(folderName);
                }
            }

            return availableNames;
        }


        public GPUMeshBatch LoadGLTF(string gltfFilePath)
        {
            GPUMeshBatch meshBatch = new GPUMeshBatch();
            meshBatch.LoadGLTF(gltfFilePath);
            return meshBatch;
        }
    }

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

            // Define the 12 triangles using the vertices with counter-clockwise winding
            Triangle[] triangles = new Triangle[]
            {
            // Front face
            new Triangle(vertices[0], vertices[1], vertices[2]),
            new Triangle(vertices[2], vertices[3], vertices[0]),
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

    // represents a single mesh from a batch on the GPU
    public struct dMeshTicket
    {
        public int StartIndex;
        public int TriangleCount;
        public Mat4x4 matrix;
        public Mat4x4 modelMatrix;
        public int meshIndex; // index in the dMeshTicket arrays

        public dMeshTicket(int meshIndex, int startIndex, int triangleCount, Mat4x4 matrix, Mat4x4 modelMatrix)
        {
            this.meshIndex = meshIndex;
            this.StartIndex = startIndex;
            this.TriangleCount = triangleCount;
            this.matrix = matrix;
            this.modelMatrix = modelMatrix;
        }
    }

    // represents all the meshs on the CPU
    public class GPUMeshBatch
    {
        public int triangleCount = 0;
        // To store multiple meshes in a flat structure
        private Triangle[] meshTrianglesCPU;
        private dMeshTicket[] meshTickets;
        private (Vec3 pos, Vec3 rotDegrees, Vec3 scale)[] transformations;

        // GPU Buffers
        private MemoryBuffer1D<Triangle, Stride1D.Dense> triangles;
        private MemoryBuffer1D<TransformedTriangle, Stride1D.Dense> workingTriangles;
        private MemoryBuffer1D<dMeshTicket, Stride1D.Dense> meshTicketBuffer;

        // Dirty flags
        private bool isMeshTicketsDirty;
        private bool isTrianglesDirty;

        public GPUMeshBatch()
        {
            meshTrianglesCPU = new Triangle[0];
            meshTickets = new dMeshTicket[0];
            transformations = new (Vec3 pos, Vec3 rotDegrees, Vec3 scale)[0];
        }

        public int AddMesh(GPUMesh mesh)
        {
            int startIndex = meshTrianglesCPU.Length;
            Array.Resize(ref meshTrianglesCPU, meshTrianglesCPU.Length + mesh.trianglesCPU.Length);
            Array.Copy(mesh.trianglesCPU, 0, meshTrianglesCPU, startIndex, mesh.trianglesCPU.Length);

            int meshIndex = meshTickets.Length;
            Array.Resize(ref transformations, transformations.Length + 1);
            transformations[meshIndex] = (mesh.pos, mesh.rotDegrees, mesh.scale);

            Array.Resize(ref meshTickets, meshTickets.Length + 1);
            meshTickets[meshIndex] = new dMeshTicket(meshIndex, startIndex, mesh.trianglesCPU.Length, default, default);

            isMeshTicketsDirty = true;
            isTrianglesDirty = true;
            triangleCount = meshTrianglesCPU.Length;

            return meshIndex;
        }

        public void LoadGLTF(string filename)
        {
            var model = ModelRoot.Load(filename);

            foreach (var mesh in model.LogicalMeshes)
            {
                foreach (var primitive in mesh.Primitives)
                {
                    var positionAccessor = primitive.GetVertices("POSITION");
                    var positionsList = positionAccessor.AsVector3Array();
                    var indicesAccessor = primitive.GetIndices();

                    var vertices = new List<Vec3>();
                    for (int i = 0; i < positionsList.Count(); ++i)
                    {
                        var vertex = positionsList[i];
                        vertices.Add(new Vec3(vertex.X, vertex.Y, vertex.Z));
                    }

                    var indices = new int[indicesAccessor.Count];
                    for (int i = 0; i < indicesAccessor.Count; ++i)
                    {
                        indices[i] = (int)indicesAccessor.ElementAt(i);
                    }

                    var triangles = new List<Triangle>();
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        var v1 = vertices[indices[i]];
                        var v2 = vertices[indices[i + 1]];
                        var v3 = vertices[indices[i + 2]];

                        triangles.Add(new Triangle(v1, v2, v3));
                    }

                    var gpuMesh = new GPUMesh(triangles);
                    gpuMesh.SetScale(0.1f, 0.1f, 0.1f);
                    this.AddMesh(gpuMesh);
                }
            }
        }

        public void SetPos(int meshID, float x, float y, float z)
        {
            transformations[meshID] = (new Vec3(x, y, z), transformations[meshID].rotDegrees, transformations[meshID].scale);
            UpdateMeshTicket(meshID);
        }

        public void SetRot(int meshID, float xRotDegrees, float yRotDegrees, float zRotDegrees)
        {
            transformations[meshID] = (transformations[meshID].pos, new Vec3(xRotDegrees, yRotDegrees, zRotDegrees), transformations[meshID].scale);
            UpdateMeshTicket(meshID);
        }

        public void SetScale(int meshID, float x, float y, float z)
        {
            transformations[meshID] = (transformations[meshID].pos, transformations[meshID].rotDegrees, new Vec3(x, y, z));
            UpdateMeshTicket(meshID);
        }

        private void UpdateMeshTicket(int meshID)
        {
            var (pos, rotDegrees, scale) = transformations[meshID];
            var modelMatrix = Mat4x4.CreateModelMatrix(pos, rotDegrees, scale);
            meshTickets[meshID] = new dMeshTicket(meshID, meshTickets[meshID].StartIndex, meshTickets[meshID].TriangleCount, meshTickets[meshID].matrix, modelMatrix);
        }

        public void ApplyCamera(Mat4x4 cameraMatrix)
        {
            isMeshTicketsDirty = true;

            for (int i = 0; i < meshTickets.Length; ++i)
            {
                UpdateMeshTicket(i);
                var modelMatrix = meshTickets[i].modelMatrix;
                var matrix = cameraMatrix * modelMatrix;
                meshTickets[i] = new dMeshTicket(i, meshTickets[i].StartIndex, meshTickets[i].TriangleCount, matrix, modelMatrix);
            }
        }

        public dMeshBatch toGPU(GPU.Renderer gpu)
        {
            // Check if GPU memory needs to be reallocated
            if (triangles == null || triangles.Length != meshTrianglesCPU.Length || isTrianglesDirty)
            {
                triangles?.Dispose();
                workingTriangles?.Dispose();
                meshTicketBuffer?.Dispose();

                triangles = gpu.device.Allocate1D(meshTrianglesCPU);
                workingTriangles = gpu.device.Allocate1D<TransformedTriangle>(meshTrianglesCPU.Length);
                meshTicketBuffer = gpu.device.Allocate1D(meshTickets);

                isTrianglesDirty = false;
                isMeshTicketsDirty = false;
            }
            else
            {
                if (isTrianglesDirty)
                {
                    triangles.CopyFromCPU(meshTrianglesCPU);
                    isTrianglesDirty = false;
                }

                if (isMeshTicketsDirty)
                {
                    meshTicketBuffer.CopyFromCPU(meshTickets);
                    isMeshTicketsDirty = false;
                }
            }

            return new dMeshBatch(triangles, workingTriangles, meshTicketBuffer);
        }
    }

    // represents a batch of meshs on the gpu
    public struct dMeshBatch
    {
        public ArrayView1D<Triangle, Stride1D.Dense> triangles;
        public ArrayView1D<TransformedTriangle, Stride1D.Dense> workingTriangles;
        public ArrayView1D<dMeshTicket, Stride1D.Dense> meshTickets;

        public dMeshBatch(ArrayView1D<Triangle, Stride1D.Dense> triangles,
                          ArrayView1D<TransformedTriangle, Stride1D.Dense> workingTriangles,
                          ArrayView1D<dMeshTicket, Stride1D.Dense> meshTickets)
        {
            this.triangles = triangles;
            this.workingTriangles = workingTriangles;
            this.meshTickets = meshTickets;
        }

        public Triangle GetTriangle(int meshID, int meshTriangleIndex)
        {
            dMeshTicket ticket = meshTickets[meshID];
            if (meshTriangleIndex >= 0 && meshTriangleIndex < ticket.TriangleCount)
            {
                int globalIndex = ticket.StartIndex + meshTriangleIndex;
                return triangles[globalIndex];
            }
            return default;
        }

        public void SetWorkingTriangle(int meshID, int meshTriangleIndex, TransformedTriangle newTriangle)
        {
            dMeshTicket ticket = meshTickets[meshID];
            if (meshTriangleIndex >= 0 && meshTriangleIndex < ticket.TriangleCount)
            {
                int globalIndex = ticket.StartIndex + meshTriangleIndex;
                workingTriangles[globalIndex] = newTriangle;
            }
        }

        public void SetWorkingTriangle(int index, TransformedTriangle newTriangle)
        {
            if (index >= 0 && index < workingTriangles.Length)
            {
                workingTriangles[index] = newTriangle;
            }
        }

        public TransformedTriangle GetWorkingTriangle(int meshID, int meshTriangleIndex)
        {
            dMeshTicket ticket = meshTickets[meshID];
            if (meshTriangleIndex >= 0 && meshTriangleIndex < ticket.TriangleCount)
            {
                int globalIndex = ticket.StartIndex + meshTriangleIndex;
                return workingTriangles[globalIndex];
            }
            return default; 
        }

        public dMeshTicket GetMeshTicketByTriangleIndex(int triangleIndex)
        {
            // Walk the meshTickets list to find the correct mesh
            int globalIndex = 0;
            for (int i = 0; i < meshTickets.Length; i++)
            {
                if (triangleIndex < globalIndex + meshTickets[i].TriangleCount)
                {
                    return meshTickets[i];
                }
                globalIndex += meshTickets[i].TriangleCount;
            }
            return default; // Return default value if the triangleIndex is not found
        }

        public int GetLocalIndexByTriangleIndex(int triangleIndex)
        {
            int globalIndex = 0;
            for (int i = 0; i < meshTickets.Length; i++)
            {
                if (triangleIndex < globalIndex + meshTickets[i].TriangleCount)
                {
                    int localTriangleIndex = triangleIndex - globalIndex;
                    return localTriangleIndex;
                }
                globalIndex += meshTickets[i].TriangleCount;
            }
            return -1; // Return invalid indices if the triangleIndex is not found
        }


        public dMeshTicket GetMeshTicket(int meshID)
        {
            return meshTickets[meshID];
        }

        public Triangle GetTriangle(int triangleIndex)
        {
            // Walk the meshTickets list to find the correct mesh and local index
            int globalIndex = 0;
            for (int i = 0; i < meshTickets.Length; i++)
            {
                if (triangleIndex < globalIndex + meshTickets[i].TriangleCount)
                {
                    return triangles[triangleIndex];
                }
                globalIndex += meshTickets[i].TriangleCount;
            }
            return default;
        }

        public TransformedTriangle GetWorkingTriangle(int triangleIndex)
        {
            // Walk the meshTickets list to find the correct mesh and local index
            int globalIndex = 0;
            for (int i = 0; i < meshTickets.Length; i++)
            {
                if (triangleIndex < globalIndex + meshTickets[i].TriangleCount)
                {
                    return workingTriangles[triangleIndex];
                }
                globalIndex += meshTickets[i].TriangleCount;
            }
            return default;
        }
    }
}
