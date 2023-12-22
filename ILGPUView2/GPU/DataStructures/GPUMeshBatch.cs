using ILGPU.Runtime;
using ILGPU;
using System;
using System.Collections.Generic;
using System.Linq;
using SharpGLTF.Schema2;

namespace GPU
{
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

                    // Check if the UV accessor is available
                    var uvAccessor = primitive.GetVertices("TEXCOORD_0");
                    var hasUVs = uvAccessor != null;

                    var triangles = new List<Triangle>();
                    for (int i = 0; i < indicesAccessor.Count; i += 3)
                    {
                        var v1 = positionsList[(int)indicesAccessor.ElementAt(i)];
                        var v2 = positionsList[(int)indicesAccessor.ElementAt(i + 1)];
                        var v3 = positionsList[(int)indicesAccessor.ElementAt(i + 2)];

                        Vec2 uv1, uv2, uv3;
                        if (hasUVs)
                        {
                            uv1 = uvAccessor.AsVector2Array()[(int)indicesAccessor.ElementAt(i)];
                            uv2 = uvAccessor.AsVector2Array()[(int)indicesAccessor.ElementAt(i + 1)];
                            uv3 = uvAccessor.AsVector2Array()[(int)indicesAccessor.ElementAt(i + 2)];
                        }
                        else
                        {
                            uv1 = uv2 = uv3 = new Vec2(0, 0); // Default UVs
                        }

                        triangles.Add(new Triangle(new Vec3(v1.X, v1.Y, v1.Z), new Vec3(v2.X, v2.Y, v2.Z), new Vec3(v3.X, v3.Y, v3.Z), uv1, uv2, uv3));
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

        public TransformedTriangle GetWorkingTriangle(int globalIndex)
        {
            if (globalIndex >= 0 && globalIndex < workingTriangles.Length)
            {
                return workingTriangles[globalIndex];
            }

            return default;
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

        public int GetLocalIndexByTriangleIndex(int meshID, int triangleIndex)
        {
            return triangleIndex - meshTickets[meshID].StartIndex;
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
    }
}
