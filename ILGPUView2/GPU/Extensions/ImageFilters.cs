using GPU;
using ILGPU;
using ILGPU.Runtime;
using ILGPUView2.GPU.DataStructures;
using ILGPUView2.GPU.Filters;
using ILGPUView2.GPU.RT;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static GPU.Kernels;

namespace GPU
{
    public interface IImageFilter
    {
        RGBA32 Apply(int tick, float x, float y, dImage output);
    }

    public interface IImageMask
    {
        RGBA32 Apply(int tick, float x, float y, dImage output, dImage input);
    }

    public interface IMegaTextureMask
    {
        RGBA32 Apply(int tick, float x, float y, dImage output, dMegaTexture input);
    }

    public interface IIntImageMask
    {
        RGBA32 Apply(int tick, int x, int y, dImage output, dImage input);
    }

    public interface ITexturedMask
    {
        RGBA32 Apply(int tick, float x, float y, dImage output, dImage mask, dImage texture);
    }

    public interface I3TextureMask
    {
        RGBA32 Apply(int tick, int x, int y, dImage output, dImage mask, dImage texture0, dImage texture1);
    }


    public static partial class Kernels
    {
        public static void ImageFilterKernel<TFunc>(Index1D index, int tick, dImage output, TFunc filter) where TFunc : unmanaged, IImageFilter
        {
            int x = index.X % output.width;
            int y = index.X / output.width;

            double u = (double)x / (double)output.width;
            double v = (double)y / (double)output.height;

            RGBA32 color = filter.Apply(tick, (float)u, (float)v, output);

            if(color.a != 0)
            {
                output.SetColorAt(x, y, color);
            }
            else
            {
                output.SetColorAt(x, y, new RGBA32(0, 0, 0));
            }
        }


        public static void ImageMaskKernel<TFunc>(Index1D index, int tick, dImage output, dImage input, TFunc filter) where TFunc : unmanaged, IImageMask
        {
            int x = index.X % output.width;
            int y = index.X / output.width;

            double u = (double)x / (double)output.width;
            double v = (double)y / (double)output.height;

            output.SetColorAt(x, y, filter.Apply(tick, (float)u, (float)v, output, input));
        }

        public static void MegaTextureMaskKernel<TFunc>(Index1D index, int tick, dImage output, dMegaTexture input, TFunc filter) where TFunc : unmanaged, IMegaTextureMask
        {
            int x = index.X % output.width;
            int y = index.X / output.width;

            double u = (double)x / (double)output.width;
            double v = (double)y / (double)output.height;

            output.SetColorAt(x, y, filter.Apply(tick, (float)u, (float)v, output, input));
        }

        public static void IntImageMaskKernel<TFunc>(Index1D index, int tick, dImage output, dImage input, TFunc filter) where TFunc : unmanaged, IIntImageMask
        {
            int x = index.X % output.width;
            int y = index.X / output.width;

            output.SetColorAt(x, y, filter.Apply(tick, x, y, output, input));
        }

        public static void FilteredDepthKernel(Index1D index, dImage output, FilterDepth filter)
        {
            int x = index.X % output.width;
            int y = index.X / output.width;

            output.SetColorAt(x, y, filter.Apply(x, y, output));
        }

        public static void TexturedMaskKernel<TFunc>(Index1D index, int tick, dImage output, dImage mask, dImage texture, TFunc filter) where TFunc : unmanaged, ITexturedMask
        {
            int x = index.X % output.width;
            int y = index.X / output.width;

            double u = (double)x / (double)output.width;
            double v = (double)y / (double)output.height;

            output.SetColorAt(x, y, filter.Apply(tick, (float)u, (float)v, output, mask, texture));
        }

        public static void ThreeTextureMaskKernel<TFunc>(Index1D index, int tick, dImage output, dImage mask, dImage texture0, dImage texture1, TFunc filter) where TFunc : unmanaged, I3TextureMask
        {
            int x = index.X % output.width;
            int y = index.X / output.width;

            output.SetColorAt(x, y, filter.Apply(tick, x, y, output, mask, texture0, texture1));
        }

    }

    public partial class Renderer
    {
        public void ExecuteFilter<TFunc>(GPUImage output, TFunc filter = default) where TFunc : unmanaged, IImageFilter
        {
            var kernel = GetFilterKernel(filter);
            kernel(output.width * output.height, ticks, output.toDevice(this), filter);
        }

        private Action<Index1D, int, dImage, TFunc> GetFilterKernel<TFunc>(TFunc filter = default) where TFunc : unmanaged, IImageFilter
        {
            if (!kernels.ContainsKey(filter.GetType()))
            {
                var kernel = device.LoadAutoGroupedStreamKernel<Index1D, int, dImage, TFunc>(ImageFilterKernel);
                kernels.Add(filter.GetType(), kernel);
            }

            return (Action<Index1D, int, dImage, TFunc>)kernels[filter.GetType()];
        }

        public void ExecuteMask<TFunc>(GPUImage output, GPUImage input, TFunc filter = default) where TFunc : unmanaged, IImageMask
        {
            var kernel = GetMaskKernel(filter);
            kernel(output.width * output.height, ticks, output.toDevice(this), input.toDevice(this), filter);
        }

        private Action<Index1D, int, dImage, dImage, TFunc> GetMaskKernel<TFunc>(TFunc filter = default) where TFunc : unmanaged, IImageMask
        {
            if (!kernels.ContainsKey(filter.GetType()))
            {
                Action<Index1D, int, dImage, dImage, TFunc> kernel = device.LoadAutoGroupedStreamKernel<Index1D, int, dImage, dImage, TFunc>(ImageMaskKernel);
                kernels.Add(filter.GetType(), kernel);
            }

            return (Action<Index1D, int, dImage, dImage, TFunc>)kernels[filter.GetType()];
        }

        public void ExecuteMegaTextureMask<TFunc>(GPUImage output, GPUMegaTexture input, TFunc filter = default) where TFunc : unmanaged, IMegaTextureMask
        {
            var kernel = GetMegaTextureMaskKernel(filter);
            kernel(output.width * output.height, ticks, output.toDevice(this), input.ToGPU(this), filter);
        }

        private Action<Index1D, int, dImage, dMegaTexture, TFunc> GetMegaTextureMaskKernel<TFunc>(TFunc filter = default) where TFunc : unmanaged, IMegaTextureMask
        {
            if (!kernels.ContainsKey(filter.GetType()))
            {
                Action<Index1D, int, dImage, dMegaTexture, TFunc> kernel = device.LoadAutoGroupedStreamKernel<Index1D, int, dImage, dMegaTexture, TFunc>(MegaTextureMaskKernel);
                kernels.Add(filter.GetType(), kernel);
            }

            return (Action<Index1D, int, dImage, dMegaTexture, TFunc>)kernels[filter.GetType()];
        }

        public void ExecuteIntMask<TFunc>(GPUImage output, GPUImage input, TFunc filter = default) where TFunc : unmanaged, IIntImageMask
        {
            var kernel = GetIntMaskKernel(filter);
            kernel(output.width * output.height, ticks, output.toDevice(this), input.toDevice(this), filter);
        }

        private Action<Index1D, int, dImage, dImage, TFunc> GetIntMaskKernel<TFunc>(TFunc filter = default) where TFunc : unmanaged, IIntImageMask
        {
            if (!kernels.ContainsKey(filter.GetType()))
            {
                Action<Index1D, int, dImage, dImage, TFunc> kernel = device.LoadAutoGroupedStreamKernel<Index1D, int, dImage, dImage, TFunc>(IntImageMaskKernel);
                kernels.Add(filter.GetType(), kernel);
            }

            return (Action<Index1D, int, dImage, dImage, TFunc>)kernels[filter.GetType()];
        }

        public void ExecuteTexturedMask<TFunc>(GPUImage output, GPUImage mask, GPUImage texture, TFunc filter = default) where TFunc : unmanaged, ITexturedMask
        {
            var kernel = GetTexturedMaskKernel(filter);
            kernel(output.width * output.height, ticks, output.toDevice(this), mask.toDevice(this), texture.toDevice(this), filter);
        }

        private Action<Index1D, int, dImage, dImage, dImage, TFunc> GetTexturedMaskKernel<TFunc>(TFunc filter = default) where TFunc : unmanaged, ITexturedMask
        {
            if (!kernels.ContainsKey(filter.GetType()))
            {
                Action<Index1D, int, dImage, dImage, dImage, TFunc> kernel = device.LoadAutoGroupedStreamKernel<Index1D, int, dImage, dImage, dImage, TFunc>(TexturedMaskKernel);
                kernels.Add(filter.GetType(), kernel);
            }

            return (Action<Index1D, int, dImage, dImage, dImage, TFunc>)kernels[filter.GetType()];
        }

        public void Execute3TextureMask<TFunc>(GPUImage output, GPUImage mask, GPUImage texture0, GPUImage texture1, TFunc filter = default) where TFunc : unmanaged, I3TextureMask
        {
            var kernel = Get3TextureMaskKernel(filter);
            kernel(output.width * output.height, ticks, output.toDevice(this), mask.toDevice(this), texture0.toDevice(this), texture1.toDevice(this), filter);
        }

        private Action<Index1D, int, dImage, dImage, dImage, dImage, TFunc> Get3TextureMaskKernel<TFunc>(TFunc filter = default) where TFunc : unmanaged, I3TextureMask
        {
            if (!kernels.ContainsKey(filter.GetType()))
            {
                Action<Index1D, int, dImage, dImage, dImage, dImage, TFunc> kernel = device.LoadAutoGroupedStreamKernel<Index1D, int, dImage, dImage, dImage, dImage, TFunc>(ThreeTextureMaskKernel);
                kernels.Add(filter.GetType(), kernel);
            }

            return (Action<Index1D, int, dImage, dImage, dImage, dImage, TFunc>)kernels[filter.GetType()];
        }

    }
}
