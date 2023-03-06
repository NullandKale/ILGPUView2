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
            Vec3 pos = particles.positions[particleID];
            RGBA32 color = new RGBA32(particles.colors[particleID]);
            Vec2 pixelPos = camera.WorldToScreenPoint(pos);

            // Draw particle as a circle with color at pixel position
            int radius = particleSize;
            int startX = (int)pixelPos.x - radius;
            int startY = (int)pixelPos.y - radius;
            int endX = startX + radius * 2;
            int endY = startY + radius * 2;

            for (int y = startY; y < endY; y++)
            {
                for (int x = startX; x < endX; x++)
                {
                    // Check if pixel position is within image bounds
                    if (x >= 0 && x < output.width && y >= 0 && y < output.height)
                    {
                        // Check if pixel position is within circle bounds
                        float dx = x - pixelPos.x;
                        float dy = y - pixelPos.y;
                        float distSquared = dx * dx + dy * dy;
                        if (distSquared <= radius * radius)
                        {
                            output.SetColorAt(x, y, color);
                        }
                    }
                }
            }
        }
    }

}
