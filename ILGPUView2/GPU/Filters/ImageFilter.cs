using GPU;
using ILGPU.Algorithms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static GPU.Kernels;

namespace ILGPUView2.GPU.Filters
{
    public enum FilterType
    {
        GaussianBlur = 0,
        BoxBlur = 1,
        LaplacianOfGaussianBlur = 2,
        LaplacianBlur = 3,
        CreateSobelXKernel = 4,
        CreateSobelYKernel = 5,
        HighPass = 6,
        LowPass = 7,
        Median = 8,
        Emposs = 9,
        MotionBlur = 10,
        PrewittX = 11,
        PrewittY = 12,
        RobertsX = 13,
        RobertsY = 14,
        FreiChenX = 15,
        FreiChenY = 16,
    }

    public unsafe struct ImageFilters : IImageMask
    {
        private float sigma = 1.0f;
        private int kernelSize = 5;
        public const int maxKernelSize = 11;

        private fixed float kernel[maxKernelSize * maxKernelSize];

        public ImageFilters(float sigma, int kernelSize, FilterType type)
        {
            this.sigma = sigma;

            if (kernelSize >= maxKernelSize)
            {
                kernelSize = maxKernelSize - 1;
            }

            float[] data = new float[kernelSize * kernelSize];

            switch (type)
            {
                case FilterType.GaussianBlur:
                    this.kernelSize = kernelSize;
                    data = CreateGaussianKernel(sigma, kernelSize);
                    break;
                case FilterType.BoxBlur:
                    this.kernelSize = kernelSize;
                    data = CreateBoxBlurKernel(kernelSize);
                    break;
                case FilterType.LaplacianOfGaussianBlur:
                    this.kernelSize = kernelSize;
                    data = CreateLaplacianOfGaussianKernel(sigma, kernelSize);
                    break;
                case FilterType.LaplacianBlur:
                    this.kernelSize = 3;
                    data = CreateLaplacianKernel();
                    break;
                case FilterType.CreateSobelXKernel:
                    this.kernelSize = 3;
                    data = CreateSobelXKernel();
                    break;
                case FilterType.CreateSobelYKernel:
                    this.kernelSize = 3;
                    data = CreateSobelYKernel();
                    break;
                case FilterType.HighPass:
                    this.kernelSize = kernelSize;
                    data = CreateHighPassKernel(kernelSize);
                    break;
                case FilterType.LowPass:
                    this.kernelSize = kernelSize;
                    data = CreateLowPassKernel(kernelSize);
                    break;
                case FilterType.Median:
                    this.kernelSize = kernelSize;
                    data = CreateMedianKernel(kernelSize);
                    break;
                case FilterType.Emposs:
                    this.kernelSize = 3;
                    data = CreateEmbossKernel();
                    break;
                case FilterType.MotionBlur:
                    this.kernelSize = kernelSize;
                    data = CreateMotionBlurKernel(kernelSize, sigma % 1);
                    break;
                case FilterType.PrewittX:
                    this.kernelSize = 3;
                    data = CreatePrewittXKernel();
                    break;
                case FilterType.PrewittY:
                    this.kernelSize = 3;
                    data = CreatePrewittYKernel();
                    break;
                case FilterType.RobertsX:
                    this.kernelSize = 3;
                    data = CreateRobertsXKernel();
                    break;
                case FilterType.RobertsY:
                    this.kernelSize = 3;
                    data = CreateRobertsYKernel();
                    break;
                case FilterType.FreiChenX:
                    this.kernelSize = 3;
                    data = CreateFreiChenXKernel();
                    break;
                case FilterType.FreiChenY:
                    this.kernelSize = 3;
                    data = CreateFreiChenYKernel();
                    break;
            }

            for (int i = 0; i < data.Length; i++)
            {
                kernel[i] = data[i];
            }
        }

        private float GetKernel(int x, int y)
        {
            return kernel[(y * kernelSize) + x];
        }

        // Creates a Gaussian kernel with the given sigma and size
        public static float[] CreateGaussianKernel(float sigma, int size)
        {
            float[] kernel = new float[maxKernelSize * maxKernelSize];

            float sum = 0;

            int halfSize = size / 2;
            for (int y = -halfSize; y <= halfSize; y++)
            {
                for (int x = -halfSize; x <= halfSize; x++)
                {
                    float value = (float)(1.0f / (2.0f * Math.PI * sigma * sigma) * Math.Exp(-(x * x + y * y) / (2.0f * sigma * sigma)));
                    kernel[((y + halfSize) * size) + (x + halfSize)] = value;
                    sum += value;
                }
            }

            // Normalize the kernel to ensure the sum of all values is 1
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    kernel[(y * size) + x] = kernel[(y * size) + x] / sum;
                }
            }

            return kernel;
        }

        public static float[] CreateBoxBlurKernel(int size)
        {
            float[] kernel = new float[size * size];

            for (int i = 0; i < kernel.Length; i++)
            {
                kernel[i] = 1.0f / (float)(size * size);
            }

            return kernel;
        }

        public static float[] CreateLaplacianOfGaussianKernel(float sigma, int size)
        {
            float[] kernel = new float[maxKernelSize * maxKernelSize];

            int halfSize = size / 2;
            for (int y = -halfSize; y <= halfSize; y++)
            {
                for (int x = -halfSize; x <= halfSize; x++)
                {
                    float value = (float)((x * x + y * y - 2 * sigma * sigma) / (sigma * sigma * sigma * sigma)) * (float)Math.Exp(-(x * x + y * y) / (2.0f * sigma * sigma));
                    kernel[((y + halfSize) * size) + (x + halfSize)] = value;
                }
            }

            return kernel;
        }

        public static float[] CreateLaplacianKernel()
        {
            return new float[] { 0, 1, 0, 1, -4, 1, 0, 1, 0 };
        }


        public static float[] CreateSobelXKernel()
        {
            return new float[] { -1, 0, 1, -2, 0, 2, -1, 0, 1 };
        }

        public static float[] CreateSobelYKernel()
        {
            return new float[] { -1, -2, -1, 0, 0, 0, 1, 2, 1 };
        }

        public static float[] CreateHighPassKernel(int size)
        {
            float[] kernel = new float[size * size];
            int center = size / 2;

            for (int i = 0; i < kernel.Length; i++)
            {
                kernel[i] = -1.0f / (float)(size * size);
            }

            kernel[(center * size) + center] = 1.0f - kernel.Length;

            return kernel;
        }

        public static float[] CreateLowPassKernel(int size)
        {
            float[] kernel = new float[size * size];
            float weight = 1.0f / (size * size);

            for (int i = 0; i < kernel.Length; i++)
            {
                kernel[i] = weight;
            }

            return kernel;
        }

        public static float[] CreateMedianKernel(int size)
        {
            float[] kernel = new float[size * size];

            for (int i = 0; i < kernel.Length; i++)
            {
                kernel[i] = 1.0f / (float)(size * size);
            }

            return kernel;
        }

        public static float[] CreateEmbossKernel()
        {
            return new float[] { -2, -1, 0, -1, 1, 1, 0, 1, 2 };
        }

        public static float[] CreateMotionBlurKernel(int size, float angle)
        {
            float[] kernel = new float[size * size];
            int center = size / 2;

            for (int i = 0; i < kernel.Length; i++)
            {
                kernel[i] = 0.0f;
            }

            float radians = angle * (float)Math.PI / 180.0f;
            float distance = (float)Math.Sqrt(2.0 * center * center);
            float xStep = (float)Math.Cos(radians) / distance;
            float yStep = (float)Math.Sin(radians) / distance;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dotProduct = dx * xStep + dy * yStep;

                    if (dotProduct >= 0.0f && dotProduct <= 1.0f)
                    {
                        kernel[(y * size) + x] = 1.0f / (dotProduct + 1.0f);
                    }
                }
            }

            // Normalize the kernel to ensure the sum of all values is 1
            float sum = kernel.Sum();
            for (int i = 0; i < kernel.Length; i++)
            {
                kernel[i] = kernel[i] / sum;
            }

            return kernel;
        }

        public static float[] CreatePrewittXKernel()
        {
            return new float[] { -1, 0, 1, -1, 0, 1, -1, 0, 1 };
        }

        public static float[] CreatePrewittYKernel()
        {
            return new float[] { -1, -1, -1, 0, 0, 0, 1, 1, 1 };
        }

        public static float[] CreateRobertsXKernel()
        {
            return new float[] { 1, 0, 0, 0, -1, 0, 0, 0, 0 };
        }

        public static float[] CreateRobertsYKernel()
        {
            return new float[] { 0, 1, 0, -1, 0, 0, 0, 0, 0 };
        }

        public static float[] CreateFreiChenXKernel()
        {
            return new float[] { -1, 0, 1, -XMath.Sqrt(2), 0, XMath.Sqrt(2), -1, 0, 1 };
        }

        public static float[] CreateFreiChenYKernel()
        {
            return new float[] { -1, -XMath.Sqrt(2), -1, 0, 0, 0, 1, XMath.Sqrt(2), 1 };
        }

        public RGBA32 Apply(int tick, float x, float y, dImage output, dImage input)
        {
            Vec3 sum = new Vec3();

            // Convolve the kernel with the surrounding pixels
            int halfSize = kernelSize / 2;
            for (int ky = -halfSize; ky <= halfSize; ky++)
            {
                for (int kx = -halfSize; kx <= halfSize; kx++)
                {
                    float ix = (x * output.width) + kx;
                    float iy = (y * output.height) + ky;

                    // Clamp the coordinates to the image bounds
                    ix = XMath.Clamp(ix, 0, output.width - 1);
                    iy = XMath.Clamp(iy, 0, output.height - 1);

                    Vec3 color = input.GetPixel(ix / output.width, iy / output.height);

                    float weight = GetKernel(kx + halfSize, ky + halfSize);
                    sum += color * weight;
                }
            }

            return new RGBA32(sum);
        }
    }
}
