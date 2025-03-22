using static GPU.Kernels;
using GPU;
using UIElement;
using System.Windows.Input;
using ILGPUView2.GPU.DataStructures;
using ILGPU.Runtime;
using ILGPU;

namespace ExampleProject.Modes
{
    public class SplatMode : IRenderCallback
    {
        GaussianData rawData;
        SplatData[] gaussians;
        MemoryBuffer1D<SplatData, Stride1D.Dense> splats;
        GPUFrameBuffer frameBuffer;

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
            if (frameBuffer == null || frameBuffer.width != gpu.framebuffer.width || frameBuffer.height != gpu.framebuffer.height)
            {
                frameBuffer = new GPUFrameBuffer(gpu.framebuffer.width, gpu.framebuffer.height);
            }

            gpu.ExecuteSplatFramebufferMaskKernel<SplatRenderer>(frameBuffer.toDevice(gpu), splats);
            gpu.ExecuteFramebufferMask<FrameBufferCopy>(gpu.framebuffer, frameBuffer.toDevice(gpu));
        }

        public void OnStart(Renderer gpu)
        {
            rawData = GaussianData.LoadPly("C:\\Users\\zinsl\\Downloads\\models\\2020_3.ply");

            gaussians = rawData.Flatten();

            splats = gpu.device.Allocate1D<SplatData>(gaussians);
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

    public struct SplatRenderer : ISplatFramebufferMask
    {
        Mat4x4 viewMatrix;
        Mat4x4 projMatrix;
        Vec3 hfovxyFocal;
        Vec3 cameraPos;

        public RGBA32 Apply(int tick, float x, float y, FrameBuffer output, ArrayView1D<SplatData, Stride1D.Dense> splats)
        {
            return new RGBA32();
        }
    }
}
