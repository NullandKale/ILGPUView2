using GPU;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.OpenCL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static GPU.Kernels;

namespace GPU
{
    public interface IComplexImageMask
    {
        RGBA32 Apply(int tick, float x, float y, dImage output, dImage input, ArrayView1D<ComplexNumber, Stride1D.Dense> complexData);
    }

    public static partial class Kernels
    {
        public static void ComplexMaskKernel<TFunc>(Index1D index, int tick, dImage output, dImage input, ArrayView1D<ComplexNumber, Stride1D.Dense> complexData, TFunc filter) where TFunc : unmanaged, IComplexImageMask
        {
            int x = index.X % output.width;
            int y = index.X / output.width;

            double u = (double)x / (double)output.width;
            double v = (double)y / (double)output.height;

            output.SetColorAt(x, y, filter.Apply(tick, (float)u, (float)v, output, input, complexData));
        }
    }

    public partial class Renderer
    {
        public void ExecuteComplexMask<TFunc>(GPUImage output, GPUImage input, ArrayView1D<ComplexNumber, Stride1D.Dense> complexData, TFunc filter = default) where TFunc : unmanaged, IComplexImageMask
        {
            var kernel = GetComplexMaskKernel(filter);
            kernel(output.width * output.height, ticks, output.toDevice(this), input.toDevice(this), complexData, filter);
        }

        private Action<Index1D, int, dImage, dImage, ArrayView1D<ComplexNumber, Stride1D.Dense>, TFunc> GetComplexMaskKernel<TFunc>(TFunc filter = default) where TFunc : unmanaged, IComplexImageMask
        {
            if (!kernels.ContainsKey(filter.GetType()))
            {
                Action<Index1D, int, dImage, dImage, ArrayView1D<ComplexNumber, Stride1D.Dense>, TFunc> kernel = device.LoadAutoGroupedStreamKernel<Index1D, int, dImage, dImage, ArrayView1D<ComplexNumber, Stride1D.Dense>, TFunc >(ComplexMaskKernel);
                kernels.Add(filter.GetType(), kernel);
            }

            return (Action<Index1D, int, dImage, dImage, ArrayView1D<ComplexNumber, Stride1D.Dense>, TFunc>)kernels[filter.GetType()];
        }

    }
}
