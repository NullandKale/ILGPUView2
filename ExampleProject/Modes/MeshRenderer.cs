using GPU;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static GPU.Kernels;
using System.Windows.Input;
using UIElement;

namespace ExampleProject.Modes
{
    public class MeshRenderer : IRenderCallback
    {
        public void CreateUI()
        {
            UIBuilder.Clear();
            UIBuilder.AddLabel("Debug Renderer");
        }

        public void OnKeyPressed(Key key, ModifierKeys modifiers)
        {

        }

        public void OnLateRender(Device gpu)
        {

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

        public (int xSize, int ySize, bool update) BeforeResolutionChanged(RenderWindow renderWindow, int newWidth, int newHeight)
        {
            return (newWidth, newHeight, false);
        }
    }


    public struct Triangle
    {
        Vec3 v0;
        Vec3 v1;
        Vec3 v2;
    }

    public unsafe struct MeshRendererFilter : IImageFilter
    {

        public MeshRendererFilter()
        {

        }

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
