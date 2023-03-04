using GPU;
using ILGPU.Algorithms;
using ILGPUView2.GPU;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using UIElement;
using static GPU.Kernels;

namespace ExampleProject.Modes
{
    public class Particles : IRenderCallback
    {
        public int particleCount = 100;
        public int particleSize = 2;

        HostParticleSystem particleSystem;

        public void SetMode(int mode)
        {

        }

        public void CreateUI()
        {
            UIBuilder.Clear();
            UIBuilder.AddLabel("Particle Sim");

            var particleCountLabel = UIBuilder.AddLabel("");
            UIBuilder.AddSlider(particleCountLabel, "Particle Count: ", 10, 10000, 5000, (newParticleCount) => { particleCount = (int)newParticleCount; });

            var particleSizeLabel = UIBuilder.AddLabel("");
            UIBuilder.AddSlider(particleSizeLabel, "Particle Size: ", 1, 25, 2, (newParticleSize) => { particleSize = (int)newParticleSize; });
        }

        public void OnStart(Device gpu)
        {
        }

        public void OnRender(Device gpu)
        {
            if (particleSystem == null || particleCount != particleSystem.count) 
            {
                if(particleSystem != null)
                {
                    particleSystem.Dispose();
                }

                particleSystem = new HostParticleSystem(gpu.device, particleCount, new Vec3(1, 1, 1), new Vec3(0.5, 0.5, 0.5));
            }

            gpu.ExecuteParticleSystemFilter(gpu.framebuffer, particleSystem, new ParticleRenderer(
                new Camera3D(new Vec3(0, 0, -2), new Vec3(0, 0, 0), new Vec3(0, 1, 0), 
                gpu.framebuffer.width, gpu.framebuffer.height, 40f), particleSize));
        }

        public void OnStop()
        {
            particleSystem.Dispose();
        } 
    }

    public struct ParticleRenderer : IParticleSystemFilter
    {
        public Camera3D camera;
        public int particleSize;
        public ParticleRenderer(Camera3D camera, int particleSize) 
        {
            this.camera = camera;
            this.particleSize = particleSize;
        }

        public RGBA32 Apply(int tick, float x, float y, dParticleSystem particles, dImage output)
        {
            tick %= 100;

            int pixelX = (int)(x * output.width);
            int pixelY = (int)(y * output.height);

            Vec3 color = new Vec3();

            for (int i = 0; i < particles.length; i++)
            {
                Vec3 pos = particles.positions[i] + particles.velocities[i] * tick;

                Vec2 pixelPos = camera.WorldToScreenPoint(pos);

                // Check if particle is within particleSize distance of the pixel position
                if (XMath.Abs(pixelPos.x - pixelX) <= particleSize && XMath.Abs(pixelPos.y - pixelY) <= particleSize)
                {
                    // Return the particle color if it is within range of the pixel
                    color = (color * 0.5f) + (particles.colors[i] * 0.5f);
                }
            }

            // Return a default color if no particles are within range of the pixel
            return new RGBA32(color);
        }

    }
}
