using GPU;
using ILGPU;
using ILGPU.Algorithms;
using ILGPU.Runtime;
using ILGPUView2.GPU.DataStructures;
using System;
using System.Collections.Generic;
using System.Windows.Media.TextFormatting;

namespace ExampleProject.Modes.Rasterizer
{
    public class GPUMesh
    {
        public GPU.Device device;
        public int texture_id;
        public Vec3[] vert_pos;
        public int[] triangle_indices;
        public Vec2[] vert_uvs;

        public int mesh_id;
        public Mat4x4 transform;
        public MemoryBuffer1D<Vec3, Stride1D.Dense> device_vert_pos;
        public MemoryBuffer1D<int, Stride1D.Dense> device_triangle_indices;
        public MemoryBuffer1D<Vec2, Stride1D.Dense> device_vert_uvs;

        public GPUMesh(GPU.Device device, int mesh_id, int texture_id, List<Vec3> vert_pos, List<int> triangle_indices, List<Vec2> vert_uvs, Mat4x4 transform)
        {
            this.device = device;
            this.mesh_id = mesh_id;

            this.texture_id = texture_id;
            
            this.vert_pos = vert_pos.ToArray();
            this.triangle_indices = triangle_indices.ToArray();
            this.vert_uvs = vert_uvs.ToArray();
            this.transform = transform;

            device_vert_pos = device.device.Allocate1D(this.vert_pos);
            device_triangle_indices = device.device.Allocate1D(this.triangle_indices);
            device_vert_uvs = device.device.Allocate1D(this.vert_uvs);
        }

        public GPUMesh(GPU.Device device, int mesh_id, Mat4x4 transform)
        {
            this.device = device;
            this.mesh_id = mesh_id;

            vert_pos = new Vec3[8]
            {
                new Vec3(1, 1, -1),
                new Vec3(1, -1, -1),
                new Vec3(1, 1, 1),
                new Vec3(1, -1, 1),
                new Vec3(-1, 1, -1),
                new Vec3(-1, -1, -1),
                new Vec3(-1, 1, 1),
                new Vec3(-1, -1, 1)
            };

            triangle_indices = new int[36]
            {
                0, 4, 6, 0, 6, 2, // Front
                3, 2, 6, 3, 6, 7, // Back
                1, 5, 6, 1, 6, 2, // Right
                4, 0, 3, 4, 3, 7, // Left
                4, 5, 6, 4, 6, 7, // Top
                1, 0, 2, 1, 2, 3  // Bottom
            };

            vert_uvs = new Vec2[12]
            {
                new Vec2(0.625f, 0.5f),
                new Vec2(0.875f, 0.5f),
                new Vec2(0.875f, 0.75f),
                new Vec2(0.625f, 0.75f),
                new Vec2(0.375f, 0.75f),
                new Vec2(0.625f, 1),
                new Vec2(0.375f, 1),
                new Vec2(0.375f, 0),
                new Vec2(0.625f, 0),
                new Vec2(0.625f, 0.25f),
                new Vec2(0.375f, 0.25f),
                new Vec2(0.125f, 0.5f)
            };

            device_vert_pos = device.device.Allocate1D(this.vert_pos);
            device_triangle_indices = device.device.Allocate1D(this.triangle_indices);
            device_vert_uvs = device.device.Allocate1D(this.vert_uvs);
        }


        public Vec3 GetCenter()
        {
            if (vert_pos.Length == 0)
            {
                return new Vec3(0, 0, 0);
            }

            Vec3 min = vert_pos[0];
            Vec3 max = vert_pos[0];

            foreach (Vec3 vertex in vert_pos)
            {
                min.x = Math.Min(min.x, vertex.x);
                min.y = Math.Min(min.y, vertex.y);
                min.z = Math.Min(min.z, vertex.z);

                max.x = Math.Max(max.x, vertex.x);
                max.y = Math.Max(max.y, vertex.y);
                max.z = Math.Max(max.z, vertex.z);
            }

            return (min + max) / 2;
        }

        public dMesh GetDMesh()
        {
            return new dMesh(mesh_id, texture_id, transform, device_vert_pos, device_triangle_indices, device_vert_uvs);
        }


    }

    public struct dMesh
    {
        public int mesh_id;
        public int texture_id;
        public Mat4x4 transform;
        public ArrayView1D<Vec3, Stride1D.Dense> vert_pos;
        public ArrayView1D<int, Stride1D.Dense> triangle_indices;
        public ArrayView1D<Vec2, Stride1D.Dense> vert_uvs;

        public dMesh(int mesh_id, int texture_id, Mat4x4 transform, ArrayView1D<Vec3, Stride1D.Dense> vert_pos, ArrayView1D<int, Stride1D.Dense> triangle_indices, ArrayView1D<Vec2, Stride1D.Dense> vert_uvs)
        {
            this.mesh_id = mesh_id;
            this.texture_id = texture_id;
            this.transform = transform;
            this.vert_pos = vert_pos;
            this.triangle_indices = triangle_indices;
            this.vert_uvs = vert_uvs;
        }

        public Vec3 DrawTrianglesWithRay(int x, int y, dImage framebuffer, Camera3D camera)
        {
            Ray ray = camera.GetRay((float)x / framebuffer.width, (float)y / framebuffer.height);

            Vec3 finalColor = new Vec3(0.2, 0.3, 0.8f); // Background color
            float closestT = float.MaxValue; // Keep track of the closest intersection point

            // Iterate through all the triangles
            for (int i = 0; i < triangle_indices.Length; i += 3)
            {
                // Get the vertices of the triangle
                Vec3 v0 = vert_pos[triangle_indices[i]];
                Vec3 v1 = vert_pos[triangle_indices[i + 1]];
                Vec3 v2 = vert_pos[triangle_indices[i + 2]];

                // Calculate the normal of the triangle
                Vec3 e1 = v1 - v0;
                Vec3 e2 = v2 - v0;
                Vec3 normal = Vec3.cross(e1, e2);

                // Back-face culling
                if (Vec3.dot(normal, ray.b) > 0)
                {
                    continue;
                }

                // Check if the ray intersects the plane of the triangle
                float t = Vec3.dot(normal, v0 - ray.a) / Vec3.dot(normal, ray.b);

                // Calculate the point of intersection
                Vec3 p = ray.a + t * ray.b;

                // Check if the point of intersection is inside the triangle
                Vec3 e0 = v0 - p;
                Vec3 c = Vec3.cross(e0, e1);
                if (Vec3.dot(normal, c) < 0) { continue; }
                c = Vec3.cross(e1, e2);
                if (Vec3.dot(normal, c) < 0) { continue; }
                c = Vec3.cross(e2, e0);
                if (Vec3.dot(normal, c) < 0) { continue; }

                // Calculate barycentric coordinates
                float areaABC = Vec3.dot(normal, Vec3.cross(e1, e2));
                float areaPBC = Vec3.dot(normal, Vec3.cross(p - v1, p - v2));
                float areaPCA = Vec3.dot(normal, Vec3.cross(p - v2, p - v0));
                float u = areaPBC / areaABC;
                float v = areaPCA / areaABC;
                float w = 1 - u - v;

                if (t > 0 && t < closestT) // Check if the intersection is closer than the previous one
                {
                    // Set the final color based on the UV coordinates of the point of intersection
                    Vec2 uv0 = vert_uvs[triangle_indices[i]];
                    Vec2 uv1 = vert_uvs[triangle_indices[i + 1]];
                    Vec2 uv2 = vert_uvs[triangle_indices[i + 2]];
                    Vec2 uv = uv0 * w + uv1 * u + uv2 * v;
                    finalColor = new Vec3(uv.x, 0, uv.y);
                    closestT = t;
                }
            }

            return finalColor;
        }

    }
}
