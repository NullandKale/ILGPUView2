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

namespace ExampleProject.Modes
{
    public class ImageFilter : IRenderCallback
    {
        string currentFile;
        string inputFile;
        GPUImage image;

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

        public void OnRender(GPU.Device gpu)
        {
            //UpdateImage();

            //gpu.ExecuteMask(gpu.framebuffer, image, new GaussianBlurFilter(1.0f, 5));
        }

        public void OnStart(GPU.Device gpu)
        {

        }

        public void OnStop()
        {

        }

        public void SetMode(int mode)
        {

        }
    }

    // doesnt work yet
    public struct GaussianBlurFilter : IImageMask
    {
        // Gaussian kernel parameters
        private float sigma = 1.0f;
        private int kernelSize = 5;

        public GaussianBlurFilter(float sigma, int kernelSize)
        {
            this.sigma = sigma;
            this.kernelSize = kernelSize;
        }

        // Creates a Gaussian kernel with the given sigma and size
        public static float[,] CreateGaussianKernel(float sigma, int size)
        {
            // this needs to be a LocalMemory Allocation
            float[,] kernel = new float[size, size];
            float sum = 0;

            int halfSize = size / 2;
            for (int y = -halfSize; y <= halfSize; y++)
            {
                for (int x = -halfSize; x <= halfSize; x++)
                {
                    float value = (float)(1.0f / (2.0f * Math.PI * sigma * sigma) * Math.Exp(-(x * x + y * y) / (2.0f * sigma * sigma)));
                    kernel[x + halfSize, y + halfSize] = value;
                    sum += value;
                }
            }

            // Normalize the kernel to ensure the sum of all values is 1
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    kernel[x, y] = kernel[x,y] / sum;
                }
            }

            return kernel;
        }

        //public static ArrayView2D<float, Stride2D.DenseY> CreateGaussianKernel(float sigma, int size)
        //{
        //    //float[,] kernel = new float[size, size];
        //    var kernel = LocalMemory.Allocate2D<float, Stride2D.DenseY>(new Index2D(size, size));

        //    float sum = 0;

        //    int halfSize = size / 2;
        //    for (int y = -halfSize; y <= halfSize; y++)
        //    {
        //        for (int x = -halfSize; x <= halfSize; x++)
        //        {
        //            float value = (float)(1.0f / (2.0f * Math.PI * sigma * sigma) * Math.Exp(-(x * x + y * y) / (2.0f * sigma * sigma)));
        //            kernel[x + halfSize, y + halfSize] = value;
        //            sum += value;
        //        }
        //    }

        //    // Normalize the kernel to ensure the sum of all values is 1
        //    for (int y = 0; y < size; y++)
        //    {
        //        for (int x = 0; x < size; x++)
        //        {
        //            kernel[x, y] = kernel[x, y] / sum;
        //        }
        //    }

        //    return kernel;
        //}

        public RGBA32 Apply(int tick, float x, float y, dImage output, dImage input)
        {
            Vec3 sum = new Vec3();
            float[,] kernel = CreateGaussianKernel(sigma, kernelSize);

            // Convolve the kernel with the surrounding pixels
            int halfSize = kernelSize / 2;
            for (int ky = -halfSize; ky <= halfSize; ky++)
            {
                for (int kx = -halfSize; kx <= halfSize; kx++)
                {
                    int ix = (int)(x * output.width) + kx;
                    int iy = (int)(y * output.height) + ky;

                    // Clamp the coordinates to the image bounds
                    ix = XMath.Clamp(ix, 0, output.width - 1);
                    iy = XMath.Clamp(iy, 0, output.height - 1);

                    Vec3 color = input.GetPixel(ix, iy);

                    float weight = kernel[kx + halfSize, ky + halfSize];
                    sum += color * weight;
                }
            }

            return new RGBA32(sum);
        }
    }

}
