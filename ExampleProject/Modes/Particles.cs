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

                particleSystem = new HostParticleSystem(gpu.device, particleCount, new Vec3(1, 1, 1), new Vec3(0.5, 0.5, 0.5));
            }

            gpu.ExecuteFilter(gpu.framebuffer, new Clear(new Vec3(0, 0, 0)));
            gpu.DrawParticleSystem(gpu.framebuffer, particleSystem, new ParticleRenderer(
                new Camera3D(new Vec3(0, 0, -2), new Vec3(0, 0, 0), new Vec3(0, 1, 0),
                gpu.framebuffer.width, gpu.framebuffer.height, 40f), particleSize));
        }

        public void OnStop()
        {
            particleSystem.Dispose();
        } 
    }
}
