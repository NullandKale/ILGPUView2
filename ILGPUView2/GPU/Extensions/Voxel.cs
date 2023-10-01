using GPU;
using ILGPU;
using ILGPU.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static GPU.Kernels;

namespace GPU
{
    public interface IVoxelMask
    {
        void Apply(int tick, float x, float y, float z, dVoxels voxels, dImage depth, dImage image);
    }

    public interface IVoxelFilter
    {
        RGBA32 Apply(dImage output, int tick, float x, float y, dVoxels voxels);
    }

    public static partial class Kernels
    {
        public static void VoxelFramebufferFilterKernel<TFunc>(Index2D index, int tick, dVoxels voxels, dImage depthTexture, dImage imageTexture, TFunc filter) where TFunc : unmanaged, IVoxelMask
        {
            for (int i = 0; i < voxels.zSize; i++)
            {
                float z = i / (float)voxels.zSize;
                filter.Apply(tick, index.X / (float)voxels.xSize, index.Y / (float)voxels.ySize, z, voxels, depthTexture, imageTexture);
            }
        }

        public static void VoxelFilterKernel<TFunc>(Index1D index, int tick, dVoxels voxels, dImage output, TFunc filter) where TFunc : unmanaged, IVoxelFilter
        {
            int x = index.X % output.width;
            int y = index.X / output.width;

            double u = (double)x / (double)output.width;
            double v = (double)y / (double)output.height;

            output.SetColorAt(x, y, filter.Apply(output, tick, (float)u, (float)v, voxels));
        }
    }

    public partial class Renderer
    {
        public void ExecuteVoxelFramebufferMask<TFunc>(Voxels voxels, GPUImage depth, GPUImage color, TFunc filter = default) where TFunc : unmanaged, IVoxelMask
        {
            var kernel = GetVoxelFramebufferFilterKernel(filter);
            kernel(new Index2D(voxels.xSize, voxels.ySize), ticks, voxels.toDevice(), depth.toDevice(this), color.toDevice(this), filter);
        }

        private Action<Index2D, int, dVoxels, dImage, dImage, TFunc> GetVoxelFramebufferFilterKernel<TFunc>(TFunc filter = default) where TFunc : unmanaged, IVoxelMask
        {
            if (!kernels.ContainsKey(filter.GetType()))
            {
                Action<Index2D, int, dVoxels, dImage, dImage, TFunc> kernel = device.LoadAutoGroupedStreamKernel<Index2D, int, dVoxels, dImage, dImage, TFunc>(VoxelFramebufferFilterKernel);
                kernels.Add(filter.GetType(), kernel);
            }

            return (Action<Index2D, int, dVoxels, dImage, dImage, TFunc>)kernels[filter.GetType()];
        }

        public void ExecuteVoxelFilter<TFunc>(GPUImage output, Voxels voxels, TFunc filter = default) where TFunc : unmanaged, IVoxelFilter
        {
            var kernel = GetVoxelFilterKernel(filter);
            kernel(output.width * output.height, ticks, voxels.toDevice(), output.toDevice(this), filter);
        }

        private Action<Index1D, int, dVoxels, dImage, TFunc> GetVoxelFilterKernel<TFunc>(TFunc filter = default) where TFunc : unmanaged, IVoxelFilter
        {
            if (!kernels.ContainsKey(filter.GetType()))
            {
                Action<Index1D, int, dVoxels, dImage, TFunc> kernel = device.LoadAutoGroupedStreamKernel<Index1D, int, dVoxels, dImage, TFunc>(VoxelFilterKernel);
                kernels.Add(filter.GetType(), kernel);
            }

            return (Action<Index1D, int, dVoxels, dImage, TFunc>)kernels[filter.GetType()];
        }
    }
}
