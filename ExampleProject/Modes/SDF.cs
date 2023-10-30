using static GPU.Kernels;
using GPU;
using UIElement;
using System.Windows.Input;
using System;
using System.Diagnostics;
using ILGPU.Algorithms;
using System.Reflection.Emit;

namespace ExampleProject.Modes
{
    public class SDF : IRenderCallback
    {
        SDFRenderer renderer;
        Vec3 cameraPosition = new Vec3(0, 0, -10);

        public void CreateUI()
        {
            UIBuilder.Clear();
        }

        public void OnKeyPressed(Key key, ModifierKeys modifiers)
        {
            float step = 0.1f;
            if (key == Key.W) cameraPosition.z += step;
            if (key == Key.S) cameraPosition.z -= step;
            if (key == Key.A) cameraPosition.x -= step;
            if (key == Key.D) cameraPosition.x += step;
            if (key == Key.Q) cameraPosition.y += step;
            if (key == Key.E) cameraPosition.y -= step;
        }


        public void OnLateRender(Renderer gpu)
        {

        }

        public void OnRender(Renderer gpu)
        {
            renderer.UpdateCameraPos(cameraPosition);
            gpu.ExecuteFilter(gpu.framebuffer, renderer);
        }

        public void OnStart(Renderer gpu)
        {
            renderer = new SDFRenderer();
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

    public static class SDFUtils
    {
        // Sphere SDF
        public static float SphereSDF(Vec3 p, float radius)
        {
            return p.length() - radius;
        }

        // Box SDF
        public static float BoxSDF(Vec3 p, Vec3 b)
        {
            Vec3 d = Vec3.Abs(p) - b;
            return Vec3.Max(d, new Vec3(0.0f)).length() + Math.Min(Math.Max(d.x, Math.Max(d.y, d.z)), 0.0f);
        }

        // Cylinder SDF
        public static float CylinderSDF(Vec3 p, float height, float radius)
        {
            Vec2 d = new Vec2(new Vec3(p.x, p.y, 0).length() - radius, Math.Abs(p.z) - height);
            return Math.Min(Math.Max(d.x, d.y), 0) + Vec2.Max(d, new Vec2(0.0f)).length();
        }

        // Cone SDF
        public static float ConeSDF(Vec3 p, float height, float radius)
        {
            Vec2 q = new Vec2(new Vec3(p.x, p.y, 0).length(), p.z);
            Vec2 tip = new Vec2(radius, height);
            q = Vec2.Abs(q) - tip;
            return Math.Min(Math.Max(q.x, q.y), 0) + Vec2.Max(q, new Vec2(0.0f)).length();
        }

        // Torus SDF
        public static float TorusSDF(Vec3 p, float majorRadius, float minorRadius)
        {
            Vec2 q = new Vec2(new Vec3(p.x, p.y, 0).length() - majorRadius, p.z);
            return q.length() - minorRadius;
        }

        // Union of two SDFs
        public static float SDFUnion(float d1, float d2)
        {
            return Math.Min(d1, d2);
        }

        // Intersection of two SDFs
        public static float SDFIntersection(float d1, float d2)
        {
            return Math.Max(d1, d2);
        }

        // Difference of two SDFs
        public static float SDFDifference(float d1, float d2)
        {
            return Math.Max(d1, -d2);
        }
    }


    public unsafe struct SDFRenderer : IImageFilter
    {
        const int numPrimitives = 25;

        public fixed int types[numPrimitives];
        public fixed float modelMatricies[numPrimitives * 16];
        public fixed float param1[numPrimitives];
        public fixed float param2[numPrimitives];
        public fixed float param3[numPrimitives];
        public fixed int colors[numPrimitives];

        public Vec3 cameraPos;

        public SDFRenderer()
        {
            Random rng = new Random(0);

            for (int i = 0; i < numPrimitives; i++)
            {
                types[i] = rng.Next(0, 4);  // 0 for sphere, 1 for box, 2 for cylinder, 3 for cone, 4 for torus

                Vec3 position = new Vec3((float)rng.NextDouble() * 8 - 4,
                                         (float)rng.NextDouble() * 8 - 4,
                                         (float)rng.NextDouble() * 8 - 4);

                Vec3 rotation = new Vec3((float)rng.NextDouble() * 2 * XMath.PI,
                                         (float)rng.NextDouble() * 2 * XMath.PI,
                                         (float)rng.NextDouble() * 2 * XMath.PI);

                // Create a model matrix that includes translation (position), rotation, and scale
                Mat4x4 modelMatrix = Mat4x4.CreateModelMatrix(position, rotation, new Vec3(1, 1, 1));

                for (int j = 0; j < 16; j++)
                {
                    modelMatricies[i * 16 + j] = modelMatrix.Get(j);
                }
                
                float scale = 0.5f;

                // Generate specific parameters based on type
                switch (types[i])
                {
                    case 0: // Sphere
                        param1[i] = (float)rng.NextDouble() * scale; // radius
                        break;
                    case 1: // Box
                        param1[i] = (float)rng.NextDouble() * scale; // width
                        param2[i] = (float)rng.NextDouble() * scale; // height
                        param3[i] = (float)rng.NextDouble() * scale; // depth
                        break;
                    case 2: // Cylinder
                        param1[i] = (float)rng.NextDouble() * scale; // height
                        param2[i] = (float)rng.NextDouble() * scale * 0.5f; // radius
                        break;
                    case 3: // Cone
                        param1[i] = (float)rng.NextDouble() * scale; // height
                        param2[i] = (float)rng.NextDouble(); // radius
                        break;
                    case 4: // Torus
                        param1[i] = (float)rng.NextDouble() * scale; // major radius
                        param2[i] = (float)rng.NextDouble() * scale * 0.5f; // minor radius
                        break;
                }

                colors[i] = new RGBA32((float)rng.NextDouble(), (float)rng.NextDouble(), (float)rng.NextDouble()).ToInt();
            }
        }

        public void UpdateCameraPos(Vec3 newPos)
        {
            cameraPos = newPos;
        }

        public RGBA32 Apply(int tick, float x, float y, dImage output)
        {
            float aspectRatio = (float)output.width / (float)output.height;
            Vec3 rayOrigin = cameraPos;
            Vec3 rayDir = new Vec3((x - 0.5f) * aspectRatio, y - 0.5f, 1).Normalize();

            float t = 0;
            float maxDistance = 100.0f; // Max distance for early exit

            for (int i = 0; i < 128; ++i) // Increased max steps as adaptive stepping can take smaller steps
            {
                Vec3 point = rayOrigin + rayDir * t;

                float closestDistance = float.MaxValue;
                int closestPrimitive = -1;

                for (int j = 0; j < numPrimitives; ++j)
                {
                    // modelMatricies is a fixed float[] in this struct
                    fixed (float* matrixPtr = &modelMatricies[j * 16])
                    {
                        // Mat4x4 is just a fixed float[] as well, so we can just reinterpret cast the memory
                        Mat4x4* modelMatrix = (Mat4x4*)matrixPtr;

                        Vec4 transformedPoint4D = modelMatrix->MultiplyVector(point);

                        float d = 0;
                        if (types[j] == 0)
                        {
                            d = SDFUtils.SphereSDF(transformedPoint4D, param1[j]);
                        }
                        else if (types[j] == 1)
                        {
                            d = SDFUtils.BoxSDF(transformedPoint4D, new Vec3(param1[j], param2[j], param3[j]));
                        }
                        else if (types[j] == 2)
                        {
                            d = SDFUtils.CylinderSDF(transformedPoint4D, param1[j], param2[j]);
                        }
                        else if (types[j] == 3)
                        {
                            d = SDFUtils.ConeSDF(transformedPoint4D, param1[j], param2[j]);
                        }
                        else if (types[j] == 4)
                        {
                            d = SDFUtils.TorusSDF(transformedPoint4D, param1[j], param2[j]);
                        }

                        if (d < closestDistance)
                        {
                            closestDistance = d;
                            closestPrimitive = j;
                        }
                    }
                }

                if (closestDistance < 0.001f && closestPrimitive != -1) // We hit the surface
                {
                    return new RGBA32(colors[closestPrimitive]); // Return the color of the closest hit primitive
                }

                t += closestDistance * 0.9f; // Adaptive step size

                if (t > maxDistance) // Early exit
                {
                    break;
                }
            }

            return new RGBA32(0, 0, 0); // Return black if no hit
        }

    }

}