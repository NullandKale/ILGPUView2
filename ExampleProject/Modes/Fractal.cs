using GPU;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UIElement;
using static GPU.Kernels;

namespace ExampleProject.Modes
{
    internal class Fractal : IRenderCallback
    {
        private int iterations = 100;
        private float escapeRadius = 2.0f;

        public void CreateUI()
        {
            UIBuilder.Clear();
            UIBuilder.AddLabel("Mandelbrot");

            var iterationsLabel = UIBuilder.AddLabel("");
            UIBuilder.AddSlider(iterationsLabel, "Iterations: ", 1, 500, 100, (newIterations) => { iterations = (int)newIterations; });

            var escapeRadiusLabel = UIBuilder.AddLabel("");
            UIBuilder.AddSlider(escapeRadiusLabel, "Escape Radius: ", 0.01f, 10, 2, (newRadius) => { escapeRadius = newRadius; });
        }

        public void OnRender(Device gpu)
        {
            if(gpu.framebuffer != null)
            {
                gpu.ExecuteFilter(gpu.framebuffer, new FractalRenderer(iterations, escapeRadius));
            }
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
    }

    public struct FractalRenderer : IImageFilter
    {
        private readonly int _maxIterations;
        private readonly float _escapeRadius;

        public FractalRenderer(int maxIterations, float escapeRadius)
        {
            _maxIterations = maxIterations;
            _escapeRadius = escapeRadius;
        }

        public RGBA32 Apply(int tick, float x, float y, dImage framebuffer)
        {
            tick = tick % 1000;
            double zoom = (double)Math.Pow(2, tick * 0.01f);
            double offsetX = -1.5f; // Adjust this value to change the offset on x-axis
            double offsetY = 0.0f; // Adjust this value to change the offset on y-axis
            double cx = x * 3.5f / zoom - 2.5f / zoom + offsetX;
            double cy = y * 2.0f / zoom - 1.0f / zoom + offsetY;

            double zx = 0f;
            double zy = 0f;

            int i;
            for (i = 0; i < _maxIterations; i++)
            {
                double zxNew = zx * zx - zy * zy + cx;
                double zyNew = 2f * zx * zy + cy;

                if (zxNew * zxNew + zyNew * zyNew > _escapeRadius * _escapeRadius)
                {
                    break;
                }

                zx = zxNew;
                zy = zyNew;
            }

            double t = (double)i / _maxIterations;

            double r = 9 * (1 - t) * t * t * t * 255;
            double g = 15 * (1 - t) * (1 - t) * t * t * 255;
            double b = 8.5f * (1 - t) * (1 - t) * (1 - t) * t * 255;
            return new RGBA32((byte)r, (byte)g, (byte)b, 255);
        }


    }

}
