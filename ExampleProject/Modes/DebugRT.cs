using GPU;
using GPU.RT;
using ILGPU;
using ILGPU.Algorithms;
using ILGPU.Runtime;
using System;
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

        public float focus = 0;
        public int views = 0;
        public float vFOV = 45;
        public float viewDisparity = 0;

        public DebugRT()
        {
        }

        public void OnRender(GPU.Device gpu)
        {
            if (gpu.framebuffer != null)
            {
                gpu.ExecuteSphereFilter(gpu.framebuffer, device_spheres,
                    new DebugRTFilter(gpu.ticks,
                    new Camera3D(new Vec3(0, 0, -10), new Vec3(0, 0, 0), new Vec3(0, 1, 0), 
                    gpu.framebuffer.width, gpu.framebuffer.height, 40f, new Vec3(1, 0, 1)),
                    gpu.framebuffer.width / (float)gpu.framebuffer.height,
                    focus, views, vFOV, viewDisparity));
                gpu.ExecuteMask(gpu.framebuffer, gpu.framebuffer, new TAA(0.5f));
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

        public void FocusUpdate(float val)
        {
            focus = val;
        }

        public void ViewsUpdate(float val)
        {
            views = (int)val;
        }

        public void vFOVUpdate(float val)
        {
            vFOV = val;
        }

        public void ViewDisparityUpdate(float val)
        {
            viewDisparity = val;
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
            GenerateSpheresGPT0(0);
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

            var v_L = UIBuilder.AddLabel("");
            UIBuilder.AddSlider(v_L, "Views min: 1 max: 255 current: ", 1, 255, 255, ViewsUpdate);

            var f_L = UIBuilder.AddLabel("");
            UIBuilder.AddSlider(f_L, "Focus min: -2 max: 2 current: ", -2, 2, -0.34f, FocusUpdate);

            var vFOV_L = UIBuilder.AddLabel("");
            UIBuilder.AddSlider(vFOV_L, "vFOV min: 15 max: 110 current: ", 15, 110, 40, vFOVUpdate);

            var disp_L = UIBuilder.AddLabel("");
            UIBuilder.AddSlider(disp_L, "Camera Travel min: 0 max: 1 current: ", 0, 1, 0.34f, ViewDisparityUpdate);
        }
    }

    public struct DebugRTFilter : ISphereImageFilter
    {
        private readonly int views = 255;
        private readonly float vFOV = 45;
        private readonly float viewDisparity = 0;

        private readonly float scale = 100;
        private readonly int ticksPerDay = 500;

        private readonly int tick = 0;

        private readonly Camera3D camera;
        private readonly Light sun;

        //4k
        //focus -0.656156242
        //val 90

        //portrait
        //focus -1.436937
        //val 85
        public DebugRTFilter(int tick, Camera3D camera, float contentAspect, float focus, int views, float vFOV, float viewDisparity)
        {
            this.tick = tick;
            this.views = views;
            this.vFOV = vFOV;
            this.viewDisparity = viewDisparity;

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

        Vec3 TraceBounce(ArrayView1D<Sphere, Stride1D.Dense> data, Vec3 hitNormal, Light sun, Sphere hitSphere, (Vec3 hitPoint, float t, Vec3 color, int index) hit)
        {
            uint counter = 0;
            uint seed = Utils.CreateSeed((uint)tick, counter++, hit.hitPoint.x, hit.hitPoint.y);
            float rand1 = Utils.GetRandom(seed, -1f, 1f);
            seed = Utils.CreateSeed((uint)tick, counter++, hit.hitPoint.x, hit.hitPoint.y);
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

            Vec3 bounceColor = TraceBounce(data, hitNormal, sun, hitSphere, hit);
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