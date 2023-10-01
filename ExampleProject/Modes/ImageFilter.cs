using GPU;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static GPU.Kernels;
using UIElement;
using ILGPU.Util;
using System.Drawing;
using ILGPU.Algorithms;
using ILGPU.Runtime;
using ILGPU;
using System.Windows.Controls;
using ILGPUView2.GPU.Filters;
using System.Windows.Input;

namespace ExampleProject.Modes
{
    public class ImageFilter : IRenderCallback
    {
        private string currentFile;
        private string inputFile;
        private GPUImage image;

        private float sigma;
        private int size;
        private int filter;

        public void CreateUI()
        {
            UIBuilder.Clear();
            UIBuilder.AddLabel("Debug Renderer");
            UIBuilder.AddLabel(" ");

            string[] files =
            {
                "./TestImages/Debug.png",
                "./TestImages/DebugRT.png",
                "./TestImages/GOL.png",
            };

            var inputfileLabel = UIBuilder.AddLabel("Input File: ");
            var dropdown = UIBuilder.AddDropdown(files, (selection) => 
            {
                inputfileLabel.Content = "Input File: " + files[selection];
                inputFile = files[selection];
            });
            dropdown.SelectedIndex = 0;

            var sigmaLabel = UIBuilder.AddLabel("Sigma: ");
            UIBuilder.AddSlider(sigmaLabel, "Sigma: ", 0.001f, ImageFilters.maxKernelSize, 1, (newSigma) => { sigma = newSigma; });

            var sizeLabel = UIBuilder.AddLabel("Size: ");
            UIBuilder.AddSlider(sizeLabel, "Size: ", 1, ImageFilters.maxKernelSize, 5, (newSize) => { size = (int)newSize; });

            string[] filters =
            {
                "GaussianBlur",
                "BoxBlur",
                "LaplacianOfGaussianBlur",
                "LaplacianBlur",
                "CreateSobelXKernel",
                "CreateSobelYKernel",
                "HighPass",
                "LowPass",
                "Median",
                "Emposs",
                "MotionBlur",
                "PrewittX",
                "PrewittY",
                "RobertsX",
                "RobertsY",
                "FreiChenX",
                "FreiChenY",
            };

            var filtersDropdown = UIBuilder.AddDropdown(filters, (selection) => { filter = selection; });
            filtersDropdown.SelectedIndex = 2;
        }

        private void UpdateImage()
        {
            if(currentFile == null || currentFile != inputFile) 
            {
                currentFile = inputFile;
                if (image != null)
                {
                    image.Dispose();
                }

                var bitmap = new Bitmap(currentFile);
                image = new GPUImage(bitmap);
            }
        }

        public void OnRender(GPU.Renderer gpu)
        {
            UpdateImage();

            gpu.ExecuteMask(gpu.framebuffer, image, new ImageFilters(sigma, size, (FilterType)filter));
        }

        public void OnStart(GPU.Renderer gpu)
        {

        }

        public void OnStop()
        {

        }

        public void SetMode(int mode)
        {

        }

        public void OnKeyPressed(Key key, ModifierKeys modifiers)
        {

        }

        public void OnLateRender(GPU.Renderer obj)
        {

        }

        public (int xSize, int ySize, bool update) BeforeResolutionChanged(RenderWindow renderWindow, int newWidth, int newHeight)
        {
            return (newWidth, newHeight, false);
        }
    }
}
