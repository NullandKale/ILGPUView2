using GPU;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static GPU.Kernels;
using UIElement;

namespace ExampleProject.Modes
{
    public class GOL : IRenderCallback
    {
        Device gpu;
        GPUImage[] framebuffers;

        public void CreateUI()
        {
            UIBuilder.Clear();
            UIBuilder.AddLabel("Conway's Game Of Life!");
        }

        public void OnRender(Device gpu)
        {
            this.gpu = gpu;
            ResizeFramebuffers();

            gpu.ExecuteMask<LifeMask>(framebuffers[1], framebuffers[0]);
            gpu.ExecuteMask<Scale>(gpu.framebuffer, framebuffers[1]);

            // swap framebuffers
            var t = framebuffers[0];
            framebuffers[0] = framebuffers[1];
            framebuffers[1] = t;
        }

        public void OnStart(Device gpu)
        {

        }

        public void OnStop()
        {

        }

        public void SetMode(int mode)
        {

        }

        public void ResizeFramebuffers()
        {
            bool initialized = true;

            if (framebuffers == null)
            {
                framebuffers = new GPUImage[2];
            }

            for(int i = 0; i < framebuffers.Length; i++)
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

            if(!initialized)
            {
                gpu.ExecuteFilter<LifeStartFilter>(framebuffers[0]);
            }
        }
    }

    public struct LifeStartFilter : IImageFilter
    {
        public bool isAlive(int tick, float x, float y)
        {
            uint seed = Utils.CreateSeed((uint)tick, 1, x, y);
            float randomFloat = Utils.GetRandom(seed, 0, 1);
            return randomFloat < 0.25f;
        }

        public RGBA32 Apply(int tick, float x, float y, dImage output)
        {
            if (isAlive(tick, x, y))
            {
                return new RGBA32(255, 255, 255, 255);
            }
            else
            {
                return new RGBA32(0, 0, 0, 255);
            }
        }
    }

    public struct LifeMask : IImageMask
    {
        private int IsAlive(RGBA32 color)
        {
            return color.g > (255 / 2) ? 1 : 0;
        }

        private int GetNeighborCount(int x, int y, dImage input)
        {
            int count = 0;

            count += IsAlive(input.GetColorAt(x + 1, y));
            count += IsAlive(input.GetColorAt(x - 1, y));
            count += IsAlive(input.GetColorAt(x, y + 1));
            count += IsAlive(input.GetColorAt(x, y - 1));
            count += IsAlive(input.GetColorAt(x + 1, y + 1));
            count += IsAlive(input.GetColorAt(x - 1, y - 1));
            count += IsAlive(input.GetColorAt(x - 1, y + 1));
            count += IsAlive(input.GetColorAt(x + 1, y - 1));

            return count;
        }

        public RGBA32 Apply(int tick, float x, float y, dImage output, dImage input)
        {
            // Get the number of live neighbors for this pixel
            int neighborCount = GetNeighborCount((int)(x * input.width), (int)(y * input.height), input);

            // Check if this pixel is alive or dead
            RGBA32 pixel = input.GetColorAt(x, y);
            int isAlive = IsAlive(pixel);

            // Implement the rules of Conway's Game of Life
            if (isAlive == 1 && (neighborCount < 2 || neighborCount > 3))
            {
                // Any live cell with fewer than two live neighbors dies, as if by underpopulation.
                // Any live cell with more than three live neighbors dies, as if by overpopulation.
                return new RGBA32(0, 0, 0, 255);
            }
            else if (isAlive == 0 && neighborCount == 3)
            {
                // Any dead cell with exactly three live neighbors becomes a live cell, as if by reproduction.
                return new RGBA32(255, 255, 255, 255);
            }
            else
            {
                // Otherwise, the cell stays the same.
                return pixel;
            }
        }
    }


}
