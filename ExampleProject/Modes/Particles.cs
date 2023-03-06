using GPU;
using ILGPU.Algorithms;
using ILGPUView2.GPU;
using ILGPUView2.GPU.Filters;
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
            UIBuilder.AddSlider(particleCountLabel, "Particle Count: ", 10, 1000000, 100000, (newParticleCount) => { particleCount = (int)newParticleCount; });

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

                particleSystem = new HostParticleSystem(gpu.device, particleCount, new Vec3(10, 10, 10), new Vec3(0.0005, 0.0005, 0.0005));
            }

            gpu.ExecuteFilter(gpu.framebuffer, new Clear(new Vec3(0, 0, 0)));
            gpu.ExecuteParticleSystemUpdate(particleSystem, new ParticleUpdate(-0.00001f, 0.9f, new Vec3(-10, -10, -10), new Vec3(10, 10, 10)));
            gpu.DrawParticleSystem(gpu.framebuffer, particleSystem, new ParticleRenderer(
                new Camera3D(new Vec3(0, 0, -16), new Vec3(0, 0, 0), new Vec3(0, 1, 0),
                gpu.framebuffer.width, gpu.framebuffer.height, 40f), particleSize));
        }

        public void OnStop()
        {
            particleSystem.Dispose();
        } 
    }

    public struct ParticleUpdate : IParticleSystemUpdate
    {
        private readonly float gravity;
        private readonly float restitution; // coefficient of restitution
        private readonly Vec3 volumeMin; // minimum corner of constraining volume
        private readonly Vec3 volumeMax; // maximum corner of constraining volume

        public ParticleUpdate(float gravity, float restitution, Vec3 volumeMin, Vec3 volumeMax)
        {
            this.gravity = gravity;
            this.restitution = restitution;
            this.volumeMin = volumeMin;
            this.volumeMax = volumeMax;
        }

        private void UpdatePosition(int tick, ref Particle p)
        {
            // Apply gravity to particle velocity
            p.velocity = p.velocity + new Vec3(0f, -gravity, 0f);

            // Update particle position based on velocity
            p.position += p.velocity;

            // Set particle color based on velocity magnitude using HSB color space
            float velocityMagnitude = p.velocity.length();
            p.color = Vec3.HsbToRgb(new Vec3(velocityMagnitude * 60 % 360, 1f, 1f));
        }

        private void Bounce(int tick, ref Particle p)
        {
            // Constrain particle within volume
            if (p.position.x < volumeMin.x)
            {
                p.position.x = volumeMin.x;
                p.velocity.x = -p.velocity.x * (restitution + Utils.GetRandomFloat(0.1f, (uint)tick, p.position.x / volumeMax.x, p.position.z / volumeMax.z, 0));
            }
            else if (p.position.x > volumeMax.x)
            {
                p.position.x = volumeMax.x;
                p.velocity.x = -p.velocity.x * (restitution + Utils.GetRandomFloat(0.1f, (uint)tick, p.position.x / volumeMax.x, p.position.z / volumeMax.z, 0));
            }

            if (p.position.y < volumeMin.y)
            {
                p.position.y = volumeMin.y;
                p.velocity.y = -p.velocity.y * (restitution + Utils.GetRandomFloat(0.1f, (uint)tick, p.position.x / volumeMax.x, p.position.z / volumeMax.z, 0));
            }
            else if (p.position.y > volumeMax.y)
            {
                p.position.y = volumeMax.y; 
                p.velocity.y = -p.velocity.y * (restitution + Utils.GetRandomFloat(0.1f, (uint)tick, p.position.x / volumeMax.x, p.position.z / volumeMax.z, 0));
            }

            if (p.position.z < volumeMin.z)
            {
                p.position.z = volumeMin.z;
                p.velocity.z = -p.velocity.z * (restitution + Utils.GetRandomFloat(0.1f, (uint)tick, p.position.x / volumeMax.x, p.position.z / volumeMax.z, 0));
            }
            else if (p.position.z > volumeMax.z)
            {
                p.position.z = volumeMax.z;
                p.velocity.z = -p.velocity.z * (restitution + Utils.GetRandomFloat(0.1f, (uint)tick, p.position.x / volumeMax.x, p.position.z / volumeMax.z, 0));
            }
        }


        public void Update(int tick, int id, dParticleSystem particles)
        {
            Particle p = particles.Get(id);

            UpdatePosition(tick, ref p);
            Bounce(tick, ref p);

            // Update particle in particle system
            particles.Set(id, ref p);
        }
    }


}
