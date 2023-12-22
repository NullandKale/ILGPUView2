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
    internal class FluidSim : IRenderCallback
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

        public void OnRender(Renderer gpu)
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

        public void OnStart(Renderer gpu)
        {

        }

        public void OnStop()
        {

        }

        public void SetMode(int mode)
        {
            this.mode = mode;
        }

        public void OnLateRender(Renderer obj)
        {

        }

        public (int xSize, int ySize, bool update) BeforeResolutionChanged(RenderWindow renderWindow, int newWidth, int newHeight)
        {
            return (newWidth, newHeight, false);
        }
    }
}
