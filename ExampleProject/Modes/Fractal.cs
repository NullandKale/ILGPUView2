using GPU;
using ILGPU.Algorithms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using UIElement;
using static GPU.Kernels;

namespace ExampleProject.Modes
{
    internal class Fractal : IRenderCallback
    {
        private int mode = 1;

        private int iterations = 500;
        private float escapeRadius = 2.0f;
        private float juliaReal = -0.8f;
        private float juliaImaginary = 0.156f;

        public void CreateUI()
        {
            UIBuilder.Clear();
            UIBuilder.AddLabel("Fractals!");

            var iterationsLabel = UIBuilder.AddLabel("");
            UIBuilder.AddSlider(iterationsLabel, "Iterations: ", 1, 1000, iterations, (newIterations) => { iterations = (int)newIterations; });

            var escapeRadiusLabel = UIBuilder.AddLabel("");
            UIBuilder.AddSlider(escapeRadiusLabel, "Escape Radius: ", 0.01f, 10, escapeRadius, (newRadius) => { escapeRadius = newRadius; });

            var juliaRealLabel = UIBuilder.AddLabel("");
            UIBuilder.AddSlider(juliaRealLabel, "Julia Real: ", -1, 1, juliaReal, (newJuliaReal) => { juliaReal = newJuliaReal; });

            var juliaImaginaryLabel = UIBuilder.AddLabel("");
            UIBuilder.AddSlider(juliaImaginaryLabel, "Julia Imaginary: ", -1, 1, juliaImaginary, (newJuliaImaginary) => { juliaImaginary = newJuliaImaginary; });
        }

        private Vec3 maxSpeed = new Vec3(0.1f, 0.1f, 2f);
        private Vec3 maxAcceleration = new Vec3(0.1f, 0.1f, 0.1f);
        private Vec3 velocity = new Vec3();
        private Vec3 position = new Vec3();
        private float decay = 0.1f;

        public void OnKeyPressed(Key key, ModifierKeys modifiers)
        {
            if (key == Key.W)
            {
                velocity.y += maxAcceleration.y;
            }
            if (key == Key.S)
            {
                velocity.y -= maxAcceleration.y;
            }
            if (key == Key.A)
            {
                velocity.x += maxAcceleration.x;
            }
            if (key == Key.D)
            {
                velocity.x -= maxAcceleration.x;
            }
            if (key == Key.Q)
            {
                velocity.z += maxAcceleration.z;
            }
            if (key == Key.E)
            {
                velocity.z -= maxAcceleration.z;
            }

            // clamp velocity
            velocity = Vec3.Clamp(velocity, -maxSpeed, maxSpeed);
        }

        private void Update(double frameTimeMS)
        {
            if(frameTimeMS > 0)
            {
                // calculate time in seconds
                float seconds = (float)(frameTimeMS / 1000.0);

                // update position
                position += velocity * seconds;

                // apply decay
                velocity *= XMath.Pow(decay, seconds);
            }
        }

        Stopwatch timer;
        double lastFrameTime;

        public void OnRender(Device gpu)
        {
            if (timer == null)
            {
                timer = new Stopwatch();
                lastFrameTime = -1;
            }
            else
            {
                lastFrameTime = timer.Elapsed.TotalMilliseconds;
            }

            timer.Restart();

            if (gpu.framebuffer != null)
            {
                if(lastFrameTime > 0)
                {
                    Update(lastFrameTime);
                }
                gpu.ExecuteFilter(gpu.framebuffer, new FractalRenderer(position, iterations, escapeRadius, mode, juliaReal, juliaImaginary));
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
            this.mode = mode;
        }

        public void OnLateRender(Device obj)
        {

        }

        public (int xSize, int ySize, bool update) BeforeResolutionChanged(RenderWindow renderWindow, int newWidth, int newHeight)
        {
            return (newWidth, newHeight, false);
        }
    }

    public struct FractalRenderer : IImageFilter
    {
        private readonly int _maxIterations;
        private readonly double _escapeRadius;
        private readonly int _mode;
        private readonly double _juliaReal;
        private readonly double _juliaImaginary;
        private readonly Vec3 _offset;

        public FractalRenderer(Vec3 offset, int maxIterations, double escapeRadius, int mode, double juliaReal = 0, double juliaImaginary = 0)
        {
            _offset = offset;
            _maxIterations = maxIterations;
            _escapeRadius = escapeRadius;
            _mode = mode;
            _juliaReal = juliaReal;
            _juliaImaginary = juliaImaginary;
        }

        public RGBA32 GetColorAt(int i)
        {
            double t = ((double)i / _maxIterations);

            double r = 9.0 * (1.0 - t) * t * t * t * 255.0;
            double g = 15.0 * (1.0 - t) * (1.0 - t) * t * t * 255.0;
            double b = 8.5 * (1.0 - t) * (1.0 - t) * (1.0 - t) * t * 255.0;

            double max = 250;
            if(r >= max && g >= max && b >= max)
            {
                r = 0;
                g = 0;
                b = 0;
            }

            return new RGBA32((byte)r, (byte)g, (byte)b, 255);
        }

        public RGBA32 Apply(int tick, float x, float y, dImage framebuffer)
        {
            double zoom = _offset.z + 1;
            double offsetX = _offset.x + 0.75;
            double offsetY = _offset.y;

            double zx = x * 3.5 / zoom - 2.5 / zoom + offsetX;
            double zy = y * 2.0 / zoom - 1.0 / zoom + offsetY;
            double zxNew = zx;
            double zyNew = zy;
            double cx = 0;
            double cy = 0;

            for (int i = 0; i < _maxIterations; i++)
            {
                if (_mode == 0) // Mandelbrot set
                {
                    cx = x * 3.5 / zoom - 2.5 / zoom + offsetX;
                    cy = y * 2.0 / zoom - 1.0 / zoom + offsetY;
                }
                else if (_mode == 1) // Julia set
                {
                    cx = _juliaReal;
                    cy = _juliaImaginary;
                }

                zxNew = zx * zx - zy * zy + cx;
                zyNew = 2.0 * zx * zy + cy;

                if (zxNew * zxNew + zyNew * zyNew > _escapeRadius * _escapeRadius)
                {
                    return GetColorAt(i);
                }

                zx = zxNew;
                zy = zyNew;
            }

            return new RGBA32(0, 0, 0, 255);
        }

    }
}
