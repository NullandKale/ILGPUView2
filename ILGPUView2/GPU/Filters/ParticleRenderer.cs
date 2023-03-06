using GPU;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static GPU.Kernels;

namespace ILGPUView2.GPU.Filters
{
    public struct ParticleRenderer : IParticleSystemDraw
    {
        public Camera3D camera;
        public int particleSize;
        public ParticleRenderer(Camera3D camera, int particleSize)
        {
            this.camera = camera;
            this.particleSize = particleSize;
        }

        public void Draw(int tick, int particleID, dParticleSystem particles, dImage output)
        {
            tick %= 1000;

            Vec3 pos = particles.positions[particleID] + particles.velocities[particleID] * tick;
            Vec2 pixelPos = camera.WorldToScreenPoint(pos);

            // Draw particle as a square with color at pixel position
            int startX = (int)pixelPos.x - particleSize;
            int startY = (int)pixelPos.y - particleSize;
            int endX = startX + particleSize * 2;
            int endY = startY + particleSize * 2;

            for (int y = startY; y < endY; y++)
            {
                for (int x = startX; x < endX; x++)
                {
                    // Check if pixel position is within image bounds
                    if (x >= 0 && x < output.width && y >= 0 && y < output.height)
                    {
                        RGBA32 color = new RGBA32(particles.colors[particleID]);
                        output.SetColorAt(x, y, color);
                    }
                }
            }
        }

    }
}
