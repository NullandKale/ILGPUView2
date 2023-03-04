using static GPU.Kernels;
using GPU;
using UIElement;

namespace ExampleProject.Modes
{
    public class Debug : IRenderCallback
    {
        public void CreateUI()
        {
            UIBuilder.Clear();
            UIBuilder.AddLabel("Debug Renderer");
        }

        public void OnRender(Device gpu)
        {
            gpu.ExecuteFilter<DebugFilter>(gpu.framebuffer);
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

    public struct DebugFilter : IImageFilter
    {
        public RGBA32 Apply(int tick, float x, float y, dImage framebuffer)
        {
            if ((int)(x * 10) % 2 == (int)(y * 10) % 2)
            {
                return new RGBA32((byte)(x * 255), 0, (byte)(y * 255), 255);
            }
            else
            {
                return new RGBA32(0, 0, 0, 255);
            }

        }
    }
}
