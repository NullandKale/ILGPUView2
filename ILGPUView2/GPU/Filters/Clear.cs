using GPU;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static GPU.Kernels;

namespace ILGPUView2.GPU.Filters
{
    public struct Clear : IImageFilter
    {
        private Vec3 color;

        public Clear(Vec3 color)
        {
            this.color = color;
        }

        public RGBA32 Apply(int tick, float x, float y, dImage output)
        {
            return new RGBA32(color);
        }
    }

}
