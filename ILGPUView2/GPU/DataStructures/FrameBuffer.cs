using ILGPU.Runtime;
using ILGPU;
using GPU;
using static GPU.Kernels;

namespace Camera
{
    public struct FrameBuffer
    {
        public int locked;
        public ushort minDepth;
        public ushort maxDepth;
        public int width;
        public int height;
        public int showColor;

        public ArrayView1D<byte, Stride1D.Dense> color;
        public ArrayView1D<ushort, Stride1D.Dense> depth;

        public FrameBuffer(int width, int height,
                           ushort minDepth, ushort maxDepth,
                           ArrayView1D<byte, Stride1D.Dense> color, ArrayView1D<ushort, Stride1D.Dense> depth)
        {
            locked = 0;
            showColor = 0;
            this.minDepth = minDepth;
            this.maxDepth = maxDepth;
            this.width = width;
            this.height = height;
            this.color = color;
            this.depth = depth;
        }

        public Vec3 GetColor(float x, float y)
        {
            var c = GetColorPixel((int)(x * width), (int)(y * height));

            return new Vec3(c.r / 255.0f, c.g / 255.0f, c.b / 255.0f);
        }

        public RGBA32 GetColorPixel(float x, float y)
        {
            return GetColorPixel((int)(x * width), (int)(y * height));
        }

        public RGBA32 GetColorPixel(int x, int y)
        {
            return GetColorPixel(y * width + x);
        }

        public RGBA32 GetColorPixel(int index)
        {
            index *= 4;

            if (index >= 0 && index < color.Length)
            {
                return new RGBA32(
                    color[index],
                    color[index + 1],
                    color[index + 2],
                    color[index + 3]);
            }
            else
            {
                return new RGBA32(1, 0, 1, 0);
            }
        }



        public ushort FilterDepthPixel(int x, int y, int filterWidth, int filterHeight, int fuzz)
        {
            ushort depth = GetDepthPixel(x, y);

            if (depth == 0)
            {
                float accumulator = 0;
                int count = 0;
                int fuzzCounter = 0;

                int xMax = x + filterWidth;
                int xMin = x - filterWidth;

                int yMax = y + filterHeight;
                int yMin = y - filterHeight;

                for (int i = y; i < yMax; i++)
                {
                    ushort c = GetDepthPixel(x, i);
                    if (c != 0)
                    {
                        accumulator += c;
                        count++;
                        fuzzCounter++;
                        if (fuzzCounter > fuzz)
                        {
                            i = yMax;
                        }
                    }

                }

                fuzzCounter = 0;

                for (int i = y; i > yMin; i--)
                {
                    ushort c = GetDepthPixel(x, i);
                    if (c != 0)
                    {
                        accumulator += c;
                        count++;
                        fuzzCounter++;
                        if (fuzzCounter > fuzz)
                        {
                            i = yMin;
                        }
                    }
                }

                fuzzCounter = 0;

                for (int i = x; i < xMax; i++)
                {
                    ushort c = GetDepthPixel(i, y);
                    if (c != 0)
                    {
                        accumulator += c;
                        count++;
                        fuzzCounter++;
                        if (fuzzCounter > fuzz)
                        {
                            i = xMax;
                        }
                    }
                }

                fuzzCounter = 0;

                for (int i = x; i > xMin; i--)
                {
                    ushort c = GetDepthPixel(i, y);
                    if (c != 0)
                    {
                        accumulator += c;
                        count++;
                        fuzzCounter++;
                        if (fuzzCounter > fuzz)
                        {
                            i = xMin;
                        }
                    }
                }

                return (ushort)(accumulator / count);
            }

            return depth;
        }

        public float GetDepthPixel(float x, float y)
        {
            float xCord = x * width;
            float yCord = y * height;

            ushort depth = GetDepthPixel((int)xCord, (int)yCord);

            float toReturn = Utils.Remap(depth, ushort.MinValue, ushort.MaxValue, 0f, 1f);

            return toReturn;
        }

        public float GetDepthPixel(float x, float y, int samples)
        {
            float xCord = x * width;
            float yCord = y * height;

            return GetDepthPixel((int)xCord, (int)yCord, samples);
        }

        public ushort GetDepth(float x, float y)
        {
            float xCord = x * width;
            float yCord = y * height;

            return GetDepthPixel((int)xCord, (int)yCord);
        }

        public (byte high, byte low) GetDepthBytes(float x, float y)
        {
            float xCord = x * width;
            float yCord = y * height;

            ushort depth = GetDepthPixel((int)xCord, (int)yCord);

            byte upperByte = (byte)(depth >> 8);
            byte lowerByte = (byte)(depth & 0xFF);

            return (upperByte, lowerByte);
        }

        public float GetDepthPixel(int xCord, int yCord, int samples)
        {
            double total = 0;
            double count = 0;
            ushort min = 0;
            ushort max = (ushort)(ushort.MaxValue * 1.0f);

            for (int x = -samples; x <= samples; x++)
            {
                for (int y = -samples; y <= samples; y++)
                {
                    ushort val = GetDepthPixel(xCord + x, yCord + y);

                    if(val > min && val <= max)
                    {
                        total += val;
                        count++;
                    }
                }
            }

            if(count == 0)
            {
                total = 1;
            }

            return Utils.Remap((float)(total / count), 0, 1000, 1, 0);

        }

        public ushort GetDepthPixel(int xCord, int yCord)
        {
            return GetDepthPixel(yCord * width + xCord);
        }

        public void SetDepthPixel(int xCord, int yCord, ushort val)
        {
            SetDepthPixel(yCord * width + xCord, val);
        }

        public void SetDepthPixel(int index, ushort val)
        {
            if (index >= 0 && index < depth.Length)
            {
                depth[index] = val;
            }
        }

        public ushort GetDepthPixel(int index)
        {
            if (index >= 0 && index < depth.Length)
            {
                ushort val = depth[index];

                if(val > 1000)
                {
                    val = 0;
                }

                return val;
            }
            else
            {
                return 0;
            }
        }
    }

    public struct FrameBufferCopy : IFramebufferMask
    {
        int showColor = 0;

        public FrameBufferCopy(bool showColor)
        {
            if(showColor)
            {
                this.showColor = 1;
            }
            else
            {
                this.showColor = 0;
            }
        }

        public RGBA32 Apply(int tick, float x, float y, dImage output, FrameBuffer input)
        {
            if(showColor == 1)
            {
                return input.GetColorPixel(x, y);
            }
            else
            {
                return new RGBA32(input.GetDepthPixel(x, y));
            }
        }
    }
}
