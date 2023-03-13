using Camera;
using ExampleProject.Modes;
using GPU;
using GPU.RT;
using ILGPU;
using ILGPU.Algorithms;
using ILGPU.Runtime;
using ILGPUView2.GPU.Filters;
using System;
using System.Windows.Input;
using UIElement;
using static GPU.Kernels;
using Light = GPU.RT.Light;

namespace Modes
{
    public class DebugRT : IRenderCallback
    {
        public int state = 1;

        public Accelerator device;
        public Random rng;
        public Sphere[] host_spheres;
        public MemoryBuffer1D<Sphere, Stride1D.Dense> device_spheres;

        GPUImage[] framebuffers;

        public void OnRender(GPU.Device gpu)
        {
            if (gpu.framebuffer != null)
            {
                ResizeFramebuffers(gpu);

                gpu.ExecuteSphereFilter(framebuffers[0], device_spheres,
                    new DebugRTFilter(gpu.ticks,
                    new Camera3D(new Vec3(0, 0, -10), new Vec3(0, 0, 0), new Vec3(0, 1, 0), 
                    gpu.framebuffer.width, gpu.framebuffer.height, 40f)));
                gpu.ExecuteMask(gpu.framebuffer, framebuffers[0], new TAA(0.2f));
            }
        }

        private void ResizeFramebuffers(GPU.Device gpu)
        {
            bool initialized = true;

            if (framebuffers == null)
            {
                framebuffers = new GPUImage[1];
            }

            for (int i = 0; i < framebuffers.Length; i++)
            {
                if (framebuffers[i] == null || framebuffers[i].width != gpu.framebuffer.width || framebuffers[i].height != gpu.framebuffer.height)
                {
                    if (framebuffers[i] != null)
                    {
                        framebuffers[i].Dispose();
                    }

                    framebuffers[i] = new GPUImage(gpu.renderFrame.width, gpu.renderFrame.height);
                    initialized = false;
                }
            }

            if (!initialized)
            {
                gpu.ExecuteFilter<LifeStartFilter>(framebuffers[0]);
            }
        }

        private Vec3 RandomVec(float scale)
        {
            return new Vec3(
                (float)rng.NextDouble() * scale,
                (float)rng.NextDouble() * scale,
                (float)rng.NextDouble() * scale
            );
        }

        public void GenerateSpheresGPT0(int count)
        {
            host_spheres = new Sphere[count + 7];

            rng = new Random(1337);

            for (int i = 0; i < count; i++)
            {
                host_spheres[i] = CreateSphere(4, 2);
            }

            host_spheres[count + 0] = new Sphere(new Vec3(1, 1, 1), new Vec3(0, 0, 0), 0.75f, 1f);
            host_spheres[count + 1] = new Sphere(new Vec3(1, 0, 0), new Vec3(-1, 0, 0), 0.25f, 0f);
            host_spheres[count + 2] = new Sphere(new Vec3(0, 0, 1), new Vec3(1, 0, 0), 0.25f, 0f);
            host_spheres[count + 3] = new Sphere(new Vec3(0, 1, 0), new Vec3(0, -1002.5, 0), 1000, 0f);
            host_spheres[count + 4] = new Sphere(new Vec3(1, 1, 0), new Vec3(0, 3, 0), 1f, 0.2f);
            host_spheres[count + 5] = new Sphere(new Vec3(0, 1, 1), new Vec3(-1, -1, 1), 0.5f, 0f);
            host_spheres[count + 6] = new Sphere(new Vec3(1, 0, 1), new Vec3(1, -1, -1), 0.3f, 0f);

            device_spheres = device.Allocate1D(host_spheres);
        }


        private Sphere CreateSphere(float scale, float min)
        {
            Vec3 center = RandomVec(scale) - new Vec3(min, min, min);
            float radius = (float)rng.NextDouble() * 0.50f;

            Vec3 color = new Vec3((float)rng.NextDouble(), (float)rng.NextDouble(), (float)rng.NextDouble());
            float reflectance = 0;

            return new Sphere(color, center, radius, reflectance);
        }

        public void OnStart(GPU.Device gpu)
        {
            this.device = gpu.device;
            
            //GenerateSpheres(50);
            GenerateSpheresGPT0(10);
        }

        public void GenerateSpheres(int count)
        {
            host_spheres = new Sphere[count + 4];

            rng = new Random(1337);

            for (int i = 0; i < count; i++)
            {
                host_spheres[i] = CreateSphere(6, 3);
            }

            host_spheres[count + 0] = new Sphere(new Vec3(1, 1, 1), new Vec3(0, 0, 0), 0.75f, 1f);
            host_spheres[count + 1] = new Sphere(new Vec3(1, 0, 0), new Vec3(-1, 0, 0), 0.25f, 0f);
            host_spheres[count + 2] = new Sphere(new Vec3(0, 0, 1), new Vec3(1, 0, 0), 0.25f, 0f);
            host_spheres[count + 3] = new Sphere(new Vec3(0, 1, 0), new Vec3(0, -1002.5, 0), 1000, 0f);

            device_spheres = device.Allocate1D(host_spheres);
        }

        public void OnStop()
        {

        }

        public void SetMode(int mode)
        {
            state = mode;
        }

        public void CreateUI()
        {
            UIBuilder.Clear();

            UIBuilder.AddLabel("Debug RT");
        }

        public void OnKeyPressed(Key key, ModifierKeys modifiers)
        {

        }
    }

    public struct DebugRTFilter : ISphereImageFilter
    {
        private readonly float scale = 100;
        private readonly int ticksPerDay = 500;

        private readonly int tick = 0;

        private readonly Camera3D camera;
        private readonly Light sun;

        public DebugRTFilter(int tick, Camera3D camera)
        {
            this.tick = tick;

            this.camera = camera;

            sun = GetSun();
        }

        private Light GetSun()
        {
            float time = (float)tick / ticksPerDay; // Time as a fraction of a day
            float angle = time * 2 * XMath.PI; // Convert time to an angle in radians
            float radius = scale; // Set the radius of the sun's circular path

            // Calculate the x and y coordinates of the sun's position using trigonometry
            float x = radius * XMath.Cos(angle);
            float y = radius * XMath.Sin(angle);

            Vec3 position = new Vec3(x, y, 0); // Create a vector for the sun's position
            Vec3 color = new Vec3(1, 1, 1); // Set the sun's color to white
            float intensity = 0.80f; // Set the sun's intensity
            float shadowFactor = 0.05f; // Set the sun's ambient intensity
            return new Light(position, color, intensity, shadowFactor); // Create and return the sun light
        }

        private Vec3 GetSkyColor(Camera3D cam, Light sun, Ray ray)
        {
            Vec3 sunDir = Vec3.unitVector(sun.center - cam.up);
            float angle = (float)Math.Acos(Vec3.dot(sunDir, new Vec3(0, 1, 0)));
            float lerpValue = (float)Math.Pow(1 - (angle / Math.PI), 3);
            float time = ((float)tick % ticksPerDay) / ticksPerDay;

            if (time > 0.5)
            {
                return Vec3.lerp(new Vec3(1, 0.6f, 0.2f) * sun.shadowFactor, new Vec3(0.5f, 0.7f, 1) * sun.shadowFactor, lerpValue);
            }
            else
            {
                return Vec3.lerp(new Vec3(1, 0.6f, 0.2f), new Vec3(0.5f, 0.7f, 1), lerpValue);
            }
        }

        Vec3 TraceBounce(Vec2 uv, uint counter, ArrayView1D<Sphere, Stride1D.Dense> data, Vec3 hitNormal, Light sun, Sphere hitSphere, (Vec3 hitPoint, float t, Vec3 color, int index) hit)
        {
            uint seed = Utils.CreateSeed((uint)tick, counter++, uv.x, uv.y);
            float rand1 = Utils.GetRandom(seed, -1f, 1f);
            seed = Utils.CreateSeed((uint)tick, counter++, uv.x, uv.y);
            float rand2 = Utils.GetRandom(seed, -1f, 1f);

            Vec3 bounceDir = Vec3.unitVector(hitNormal + new Vec3(rand1, rand2, 0f) * (1f - hitSphere.reflectivity));
            Ray bounceRay = new Ray(hit.hitPoint + hitNormal * 0.001f, bounceDir);
            var bounceHit = HitSphere(data, bounceRay, hit.index);

            if (bounceHit.index != -1)
            {
                // Calculate shadow ray origin and direction
                Sphere bounceHitSphere = data[bounceHit.index];
                Vec3 bounceHitNormal = Vec3.unitVector(bounceHit.hitPoint - bounceHitSphere.center);
                Vec3 shadowRayOrigin = bounceHit.hitPoint + bounceHitNormal * 0.001f;
                Vec3 shadowDir = Vec3.unitVector(sun.center - bounceHit.hitPoint);
                Ray shadowRay = new Ray(shadowRayOrigin, shadowDir);

                var shadowHit = HitSphere(data, shadowRay);
                if (shadowHit.index == -1)
                {
                    return hit.color * bounceHit.color * sun.color * sun.intensity * XMath.Pow(Vec3.dot(shadowDir, bounceHitNormal), bounceHitSphere.reflectivity);
                }
                else
                {
                    return hit.color * bounceHit.color * sun.color * sun.shadowFactor * XMath.Pow(Vec3.dot(shadowDir, bounceHitNormal), bounceHitSphere.reflectivity);
                }
            }

            return hit.color * sun.shadowFactor;
        }

        Vec3 TraceShadows(ArrayView1D<Sphere, Stride1D.Dense> data, Vec3 hitNormal, Light sun, (Vec3 hitPoint, float t, Vec3 color, int index) hit)
        {
            // Calculate sun direction
            Vec3 sunDir = Vec3.unitVector(sun.center - hit.hitPoint);

            // Calculate shadow ray origin and direction
            Vec3 shadowRayOrigin = hit.hitPoint + hitNormal * 0.001f;
            Ray shadowRay = new Ray(shadowRayOrigin, sunDir);

            var shadowHit = HitSphere(data, shadowRay);

            if (shadowHit.index != -1)
            {
                return hit.color * sun.color * sun.shadowFactor;
            }
            else
            {
                return hit.color * sun.color * sun.intensity;
            }
        }

        Vec3 Trace(Camera3D cam, Light sun, ArrayView1D<Sphere, Stride1D.Dense> data, Vec2 uv)
        {
            Ray ray = cam.GetRay(uv.x, uv.y);
            (Vec3 hitPoint, float t, Vec3 color, int index) hit = HitSphere(data, ray);

            if (hit.index == -1)
            {
                return GetSkyColor(cam, sun, ray);
            }

            Sphere hitSphere = data[hit.index];
            
            Vec3 hitNormal = Vec3.unitVector(hit.hitPoint - hitSphere.center);
            Vec3 diffuseColor = hit.color;

            Vec3 bounceColor = TraceBounce(uv, 0, data, hitNormal, sun, hitSphere, hit);
            Vec3 shadowColor = TraceShadows(data, hitNormal, sun, hit);

            return (diffuseColor * shadowColor + bounceColor);
        }

        // Called for every pixel in the output image on the GPU note.
        // Any function called inside this function CANNOT use recursion
        public RGBA32 Apply(int tick, float x, float y, dImage framebuffer, ArrayView1D<Sphere, Stride1D.Dense> spheres)
        {
            Vec3 color = Trace(camera, sun, spheres, new Vec2(x, y));
            
            return new RGBA32(color);
        }

        public (Vec3 hitPoint, float t, Vec3 color, int index) HitSphere(ArrayView1D<Sphere, Stride1D.Dense> spheres, Ray ray, int indexToIgnore = -1)
        {
            Vec3 hitPoint = new Vec3(0, 0, 0);
            Vec3 color = new Vec3(0, 0, 0);
            float tmin = float.MaxValue;
            float t = float.MaxValue;
            int index = -1;

            for (int i = 0; i < spheres.Length; i++)
            {
                if (i == indexToIgnore)
                {
                    continue;
                }

                Sphere sphere = spheres[i];
                if (sphere.Intersect(ray, out t) && t < tmin)
                {
                    index = i;
                    tmin = t;
                    hitPoint = ray.a + t * ray.b;
                    color = sphere.color;
                }
            }

            return (hitPoint, t, color, index);
        }
    }
}