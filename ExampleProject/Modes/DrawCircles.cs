using GPU;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static GPU.Kernels;
using System.Windows.Input;
using UIElement;
using ILGPU.Algorithms;

namespace ExampleProject.Modes
{
    public class DrawCircles : IRenderCallback
    {
        DrawCirclesFilter filter = new DrawCirclesFilter();

        public void CreateUI()
        {
            UIBuilder.Clear();
            UIBuilder.AddLabel("Circle Renderer");
        }

        public void OnKeyPressed(Key key, ModifierKeys modifiers)
        {

        }

        public void OnLateRender(Device obj)
        {

        }

        public void OnRender(Device gpu)
        {
            gpu.ExecuteFilter(gpu.framebuffer, filter);
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

        public (int xSize, int ySize, bool update) BeforeResolutionChanged(RenderWindow renderWindow, int newWidth, int newHeight)
        {
            return (newWidth, newHeight, false);
        }
    }

    public unsafe struct DrawCirclesFilter : IImageFilter
    {
        const int spheres = 100;
        fixed float xPositions[spheres];
        fixed float yPositions[spheres];
        fixed float radiuss[spheres];
        fixed float colorR[spheres];
        fixed float colorG[spheres];
        fixed float colorB[spheres];


        public DrawCirclesFilter()
        {
            Random rng = new Random();

            for(int i = 0; i < spheres; i++) 
            {
                xPositions[i] = (float)rng.NextDouble();
                yPositions[i] = (float)rng.NextDouble();
                radiuss[i] = (float)rng.NextDouble() * 0.1f;

                colorR[i] = (float)rng.NextDouble();
                colorG[i] = (float)rng.NextDouble();
                colorB[i] = (float)rng.NextDouble();
            }
        }

        public RGBA32 Apply(int tick, float x, float y, dImage framebuffer)
        {
            Vec3 color = new Vec3();
            int hits = 0;

            for (int i = 0; i < spheres; i++)
            {
                float xPos = xPositions[i];
                float yPos = yPositions[i];
                float radius = radiuss[i];

                Vec2 point = new Vec2(x, y);
                Vec2 center = new Vec2(xPos, yPos);

                if (Vec2.Distance(point, center) <= radius)
                {
                    color += new Vec3(colorR[i], colorG[i], colorB[i]);
                    hits++;
                }
            }

            if (hits == 0)
            {
                return new RGBA32(new Vec3(0, 0.1f, 0.6f));
            }

            return new RGBA32(color / hits);
        }

    }
}
