using GPU;
using ILGPU;
using ILGPU.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ILGPUView2.GPU
{
    public struct Particle
    {
        public int id;
        public Vec3 position;
        public Vec3 velocity;
        public Vec3 color;

        public Particle(int id, Vec3 position, Vec3 velocity, Vec3 color)
        {
            this.id = id;
            this.position = position;
            this.velocity = velocity;
            this.color = color;
        }

        public static Particle GetRandom(Random rng, Vec3 positionRange, Vec3 velocityRange)
        {
            Vec3 scale = positionRange * 2;
            Vec3 position = new Vec3(
                (float)(rng.NextDouble() * scale.x) - positionRange.x,
                (float)(rng.NextDouble() * scale.y) - positionRange.y,
                (float)(rng.NextDouble() * scale.z) - positionRange.z);

            scale = velocityRange * 2;
            Vec3 velocity = new Vec3(
                (float)(rng.NextDouble() * scale.x) - velocityRange.x,
                (float)(rng.NextDouble() * scale.y) - velocityRange.y,
                (float)(rng.NextDouble() * scale.z) - velocityRange.z);

            Vec3 color = new Vec3(rng.NextDouble(), rng.NextDouble(), rng.NextDouble());

            return new Particle(0, position, velocity, color);
        }

    }

    public class HostParticleSystem : IDisposable
    {
        public readonly int count;
        private Vec3[] positions;
        private Vec3[] velocities;
        private Vec3[] colors;

        private MemoryBuffer1D<Vec3, Stride1D.Dense> device_positions;
        private MemoryBuffer1D<Vec3, Stride1D.Dense> device_velocities;
        private MemoryBuffer1D<Vec3, Stride1D.Dense> device_colors;

        public HostParticleSystem(Accelerator device, int count, Vec3 range, Vec3 speedMax)
        {
            this.count = count;
            positions = new Vec3[count];
            velocities = new Vec3[count];
            colors = new Vec3[count];

            Random rng = new Random();
            Vec3 scale = range * 2;
            Vec3 vel_scale = speedMax * 2;

            Parallel.For(0, count, (i) =>
            {
                Vec3 position = new Vec3(
                    (float)(rng.NextDouble() * scale.x) - range.x,
                    (float)(rng.NextDouble() * scale.y) - range.y,
                    (float)(rng.NextDouble() * scale.z) - range.z);

                Vec3 velocity = new Vec3(
                    (float)(rng.NextDouble() * vel_scale.x) - speedMax.x,
                    (float)(rng.NextDouble() * vel_scale.y) - speedMax.y,
                    (float)(rng.NextDouble() * vel_scale.z) - speedMax.z);

                Vec3 color = new Vec3(rng.NextDouble(), rng.NextDouble(), rng.NextDouble());

                positions[i] = position;
                velocities[i] = velocity;
                colors[i] = color;
            });

            device_positions = device.Allocate1D(positions);
            device_velocities = device.Allocate1D(velocities);
            device_colors = device.Allocate1D(colors);
        }

        public void Dispose()
        {
            device_colors.Dispose();
            device_positions.Dispose(); 
            device_velocities.Dispose();
        }

        public dParticleSystem toGPU()
        {
            return new dParticleSystem(count, device_positions, device_velocities, device_colors);
        }
    }

    public struct dParticleSystem
    {
        public int length;
        public ArrayView1D<Vec3, Stride1D.Dense> positions;
        public ArrayView1D<Vec3, Stride1D.Dense> velocities;
        public ArrayView1D<Vec3, Stride1D.Dense> colors;

        public dParticleSystem(int length, MemoryBuffer1D<Vec3, Stride1D.Dense> positions, MemoryBuffer1D<Vec3, Stride1D.Dense> velocities, MemoryBuffer1D<Vec3, Stride1D.Dense> colors)
        {
            this.length = length;
            this.positions = positions;
            this.velocities = velocities;
            this.colors = colors;
        }

        public Particle Get(int index)
        {
            index = index % length;

            return new Particle(index, positions[index], velocities[index], colors[index]);
        }

        public void Set(int index, ref Particle p)
        {
            index = index % length;

            positions[index] = p.position;
            velocities[index] = p.velocity;
            colors[index] = p.color;
        }
    }
}
