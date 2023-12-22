using GPU;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static GPU.Kernels;
using System.Windows.Input;
using UIElement;
using ILGPU.Algorithms;
using ILGPU.Algorithms.Random;

namespace ExampleProject.Modes
{
    public class BouncingCircles : IRenderCallback
    {
        BouncingCirclesFilter filter;
        bool resetOnRenderThread = false;

        public void CreateUI()
        {
            UIBuilder.Clear();
            UIBuilder.AddLabel("Bouncing Circles");

            UIBuilder.AddSlider("Velocity Scale (Needs Reset): ", 10, 1000, 100, (val) =>
            {
                filter.velocityMagnitude = val;
            });

            UIBuilder.AddButton("Reset", () =>
            {
                resetOnRenderThread = true;
            });

            UIBuilder.AddSlider("Damping: ", 0.1f, 1f, 1f, (val) =>
            {
                filter.damping = val;
            });

            UIBuilder.AddSlider("Gravity Magnitude: ", 0, 500, 0, (val) =>
            {
                filter.gravityMagnitude = val;
            });

            UIBuilder.AddSlider("Gravity Direction: ", -1f, 1f, -0.5f, (val) =>
            {
                // -0.5 is down in the screen space
                // Map from -1 to 1 to 0 to 2π
                filter.gravityDirection = (val + 1f) * XMath.PI;
            });

        }

        public void OnKeyPressed(Key key, ModifierKeys modifiers)
        {

        }

        public void OnLateRender(Renderer obj)
        {

        }

        public void OnRender(Renderer gpu)
        {
            filter.tick = gpu.ticks;

            if(resetOnRenderThread)
            {
                filter.Reset();
                resetOnRenderThread = false;
            }

            filter.Update((float)gpu.frameTimeAverage);
            gpu.ExecuteFilter(gpu.framebuffer, filter);
        }

        public void OnStart(Renderer gpu)
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
            filter = new BouncingCirclesFilter(0, newWidth, newHeight);
            return (newWidth, newHeight, false);
        }
    }
}
