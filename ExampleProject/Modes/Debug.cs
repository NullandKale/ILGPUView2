using static GPU.Kernels;
using GPU;
using UIElement;
using System.Windows.Input;

namespace ExampleProject.Modes
{
    public class Debug : IRenderCallback
    {
        public void CreateUI()
        {
            UIBuilder.Clear();
            UIBuilder.AddLabel("Debug Renderer");
        }

        public void OnKeyPressed(Key key, ModifierKeys modifiers)
        {

        }

        public void OnLateRender(Renderer gpu)
        {

        }

        public void OnRender(Renderer gpu)
        {
            gpu.ExecuteFilter<DebugFilter>(gpu.framebuffer);
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
            return (newWidth, newHeight, false);
        }
    }

    public struct DebugFilter : IImageFilter
    {
        public RGBA32 Apply(int tick, float x, float y, dImage framebuffer)
        {
            if ((int)(x * 10) % 2 == (int)(y * 10) % 2)
            {
                // blue to red gradient
                return new RGBA32((byte)(x * 255), 0, (byte)(y * 255), 255);
            }
            else
            {
                // outputs grey color corresponding to the tick value
                return new RGBA32((byte)(tick % 255), (byte)(tick % 255), (byte)(tick % 255), 255);
            }

        }
    }
}
