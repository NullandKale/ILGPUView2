using GPU;
using ILGPU.Algorithms;
using System;
using static GPU.Kernels;

namespace ILGPUView2.GPU.Filters
{
    public struct Scale : IImageMask
    {
        public RGBA32 Apply(int tick, float u, float v, dImage output, dImage input)
        {
            return input.GetColorAt(u, v);
        }
    }
}