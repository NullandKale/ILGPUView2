using Camera;
using GPU;
using ILGPU;
using ILGPU.Algorithms;
using ILGPU.Runtime;
using ILGPUView2.GPU.DataStructures;
using ILGPUView2.GPU.Filters;
using System;
using System.Diagnostics;
using UIElement;

namespace ExampleProject.Modes.Rasterizer
{
    public static partial class Kernels
    {
        private static int Sign(int x)
        {
            return (x > 0) ? 1 : (x < 0) ? -1 : 0;
        }

        private static Vec3 NDCtoScreen(Vec3 ndc, dImage framebuffer)
        {
            int width = framebuffer.width;
            int height = framebuffer.height;

            // Perspective divide
            ndc /= ndc.z;

            // Convert from normalized device coordinates (NDC) to screen coordinates
            ndc.x = (ndc.x + 1.0f) * 0.5f * width;
            ndc.y = (1.0f - ndc.y) * 0.5f * height;

            return ndc;
        }

        private static Vec3 BarycentricCoordinates(Vec2 p, Vec3 a, Vec3 b, Vec3 c)
        {
            Vec3 v0 = new Vec3(b.x - a.x, c.x - a.x, a.x - p.x);
            Vec3 v1 = new Vec3(b.y - a.y, c.y - a.y, a.y - p.y);
            Vec3 v2 = Vec3.cross(v0, v1);

            const float epsilon = 1e-6f;

            if (Math.Abs(v2.z) < epsilon)
            {
                return new Vec3(-1, 1, 1);
            }

            return new Vec3(1.0f - (v2.x + v2.y) / v2.z, v2.y / v2.z, v2.x / v2.z);
        }

        private static float DistancePointToSegment(Vec2 point, Vec2 segmentStart, Vec2 segmentEnd)
        {
            Vec2 v = segmentEnd - segmentStart;
            Vec2 w = point - segmentStart;

            float c1 = Vec2.Dot(w, v);
            if (c1 <= 0)
            {
                return Vec2.Distance(point, segmentStart);
            }

            float c2 = Vec2.Dot(v, v);
            if (c2 <= c1)
            {
                return Vec2.Distance(point, segmentEnd);
            }

            float b = c1 / c2;
            Vec2 pb = segmentStart + b * v;
            return Vec2.Distance(point, pb);
        }

        private static Vec3 BlendColors(Vec3 color1, Vec3 color2, float weight)
        {
            float r = color1.x * weight + color2.x * (1 - weight);
            float g = color1.y * weight + color2.y * (1 - weight);
            float b = color1.z * weight + color2.z * (1 - weight);

            return new Vec3(r, g, b);
        }

        private static void DrawLine(Vec3 start, Vec3 end, Vec3 color, dImage framebuffer, int minX, int maxX, int minY, int maxY)
        {
            Vec2 segmentStart = new Vec2(start.x, start.y);
            Vec2 segmentEnd = new Vec2(end.x, end.y);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vec2 pixelCenter = new Vec2(x + 0.5f, y + 0.5f);
                    double distance = DistancePointToSegment(pixelCenter, segmentStart, segmentEnd);

                    if (distance <= 0.5)
                    {
                        Vec3 existingColor = framebuffer.GetColorAt(x, y).toVec3();
                        Vec3 blendedColor = BlendColors(color, existingColor, 0.5f);
                        framebuffer.SetColorAt(x, y, new RGBA32(blendedColor));
                    }
                }
            }
        }
        private static void RasterizeMesh(dMesh mesh, Camera3D camera, int minX, int maxX, int minY, int maxY, dImage framebuffer,
            dImage texture = default, bool wireframe = true)
        {
            Mat4x4 mvpTransform = camera.GetViewProjectionMatrixInverse();

            for (int i = 0; i < mesh.triangle_indices.Length; i += 3)
            {
                Vec3 v0 = mesh.vert_pos[mesh.triangle_indices[i]];
                Vec3 v1 = mesh.vert_pos[mesh.triangle_indices[i + 1]];
                Vec3 v2 = mesh.vert_pos[mesh.triangle_indices[i + 2]];

                Vec2 uv0 = mesh.vert_uvs[mesh.triangle_indices[i]];
                Vec2 uv1 = mesh.vert_uvs[mesh.triangle_indices[i + 1]];
                Vec2 uv2 = mesh.vert_uvs[mesh.triangle_indices[i + 2]];

                // Transform vertices to screen space
                Vec3 v0Clip = mvpTransform * v0;
                Vec3 v1Clip = mvpTransform * v1;
                Vec3 v2Clip = mvpTransform * v2;

                Vec3 v0Screen = NDCtoScreen(v0Clip, framebuffer);
                Vec3 v1Screen = NDCtoScreen(v1Clip, framebuffer);
                Vec3 v2Screen = NDCtoScreen(v2Clip, framebuffer);

                if (wireframe)
                {
                    DrawLine(v0Screen, v1Screen, new Vec3(1, 0, 1), framebuffer, minX, maxX, minY, maxY);
                    DrawLine(v1Screen, v2Screen, new Vec3(1, 0, 1), framebuffer, minX, maxX, minY, maxY);
                    DrawLine(v2Screen, v0Screen, new Vec3(1, 0, 1), framebuffer, minX, maxX, minY, maxY);
                    continue;
                }

                // Rasterize triangle
                for (int y = minY; y <= maxY; ++y)
                {
                    for (int x = minX; x <= maxX; ++x)
                    {
                        Vec3 barycentric = BarycentricCoordinates(new Vec2(x, y), v0Screen, v1Screen, v2Screen);

                        if (barycentric.x < 0 || barycentric.y < 0 || barycentric.z < 0)
                        {
                            continue;
                        }

                        // Interpolate UV coordinates
                        float u = uv0.x * barycentric.x + uv1.x * barycentric.y + uv2.x * barycentric.z;
                        float v = uv0.y * barycentric.x + uv1.y * barycentric.y + uv2.y * barycentric.z;

                        RGBA32 color = new RGBA32(new Vec3(1, 0, 1));
                        // Write to framebuffer
                        framebuffer.SetColorAt(x, y, color);
                    }
                }
            }
        }

        private static void DrawTest(int startX, int endX, int startY, int endY, dImage framebuffer)
        {
            for (int y = startY; y < endY; y++)
            {
                for (int x = startX; x < endX; x++)
                {
                    // Set the color of the pixel
                    //Vec3 color = mesh.DrawTrianglesWithRay(x, y, framebuffer, camera);
                    //Vec3 color = mesh.DrawTriangles(x, y, framebuffer, camera);
                    Vec3 color = new Vec3(1, 0, 1);
                    framebuffer.SetColorAt(x, y, new RGBA32(color));
                }
            }
        }

        public static void TriangleRasterizerKernel(Index2D index, int rows, int cols, int draw_width, int draw_height, dImage framebuffer, dImage texture, dMesh mesh, Camera3D camera)
        {
            // Calculate the screen space position and size of the rectangle
            int startX = (int)(framebuffer.width * index.X / (float)cols);
            int startY = (int)(framebuffer.height * index.Y / (float)rows);

            // Calculate the end positions of the rectangle, considering edge cases
            int endX = (int)(framebuffer.width * (index.X + 1) / (float)cols);
            int endY = (int)(framebuffer.height * (index.Y + 1) / (float)rows);

            // Clamp end positions to prevent out-of-bounds access
            endX = Math.Min(endX, framebuffer.width);
            endY = Math.Min(endY, framebuffer.height);

            //DrawTest(startX, endX, startY, endY, framebuffer);

            RasterizeMesh(mesh, camera, startX, endX, startY, endY, framebuffer, default, true);
            
        }

    }

    public static class DeviceExtensions
    {
        private static Action<Index2D, int, int, int, int, dImage, dImage, dMesh, Camera3D> triangle_raster_kernel;

        public static void DrawMesh(this GPU.Device instance, Camera3D camera, GPUImage output, GPUMesh mesh, GPUImage texture = default)
        {
            if (triangle_raster_kernel == null)
            {
                triangle_raster_kernel = instance.device.LoadAutoGroupedStreamKernel<Index2D, int, int, int, int, dImage, dImage, dMesh, Camera3D>(Kernels.TriangleRasterizerKernel);
            }

            int grid_size = 32;
            // grid_size_x and y should equal one grid cell per every 8 in output width and height respectively
            int grid_size_x = output.width / grid_size;
            int grid_size_y = output.height / grid_size;
            Index2D idx = new Index2D(grid_size_x, grid_size_y);
            if (texture == default)
            {
                // index, rows, cols, rect_width, rect_height
                triangle_raster_kernel(idx, grid_size_y, grid_size_x, grid_size, grid_size,
                       output.toDevice(instance), default, mesh.GetDMesh(), camera);
            }
            else
            {
                // index, rows, cols, rect_width, rect_height
                triangle_raster_kernel(idx, grid_size_y, grid_size_x, grid_size, grid_size, 
                    output.toDevice(instance), texture.toDevice(instance), mesh.GetDMesh(), camera);
            }
        }
    }

    public class TriangleRasterizer : IRenderCallback
    {
        public string meshFile = "./Assets/suzanne.obj";
        public GPUMesh mesh;

        public void CreateUI()
        {
            UIBuilder.Clear();
            UIBuilder.AddLabel("Triangle Rasterizer");
        }

        public void OnKeyPressed(System.Windows.Input.Key key, System.Windows.Input.ModifierKeys modifiers)
        {

        }

        Vec3 lookAt = new Vec3(0, 0, 0);

        private float cameraAngle = 0f;

        public void OnRender(GPU.Device gpu)
        {
            float cameraRadius = 3.0f; // Distance from the camera to the mesh center
            float cameraSpeed = 0.02f; // Rotation speed of the camera

            cameraAngle += cameraSpeed;

            // Calculate the camera position based on the angle
            Vec3 cameraPosition = new Vec3(
                (float)(cameraRadius * Math.Cos(cameraAngle)),
                0,
                (float)(cameraRadius * Math.Sin(cameraAngle))
            );

            Camera3D camera = new Camera3D(cameraPosition, new Vec3(0, 0, 0), new Vec3(0, 1, 0), gpu.framebuffer.width, gpu.framebuffer.height, 90f);

            gpu.ExecuteFilter<Clear>(gpu.framebuffer);
            gpu.DrawMesh(camera, gpu.framebuffer, mesh);
        }


        public void OnStart(GPU.Device gpu)
        {
            var mat = new Mat4x4();
            //mesh = MeshLoader.LoadObjFile(gpu, 0, mat, meshFile);
            mesh = new GPUMesh(gpu, 0, mat);
            lookAt = new Vec3();
        }

        public void OnStop()
        {

        }

        public void SetMode(int mode)
        {

        }
    }
}
