using static GPU.Kernels;
using GPU;
using UIElement;
using System.Windows.Input;
using ILGPUView2.GPU.DataStructures;
using System;
using System.Windows.Documents;
using System.Collections.Generic;
using System.Threading;
using ILGPU.Algorithms;
using ILGPUView2.GPU.Filters;

namespace ExampleProject.Modes
{
    public partial class MegaTextureTest : IRenderCallback
    {
        public static GPUMegaTexture loadTest()
        {
            // Define an array of file paths
            string[] files = new string[]
            {
                "./TestImages/Debug.png",
                "./TestImages/DebugRT.png",
                "./TestImages/GOL.png",
            };

            GPUMegaTexture megaTexture = new GPUMegaTexture();

            // Iterate over each file and attempt to load it as a texture
            foreach (string file in files)
            {
                if (GPUImage.TryLoad(file, out GPUImage loaded))
                {
                    megaTexture.AddTexture(loaded);
                }
            }

            return megaTexture;
        }
    }
    public partial  class MegaTextureTest : IRenderCallback
    {
        GPUMegaTexture megaTexture;
        List<TextureGenerator> textureGenerators = new List<TextureGenerator>();

        bool dirty = true;
        float slider0 = 0;
        float slider1 = 0;
        float slider2 = 0;
        float slider3 = 0;
        Vec3 colorSlider = new Vec3(1, 1, 1);

        public void CreateUI()
        {
            UIBuilder.Clear();
            UIBuilder.AddLabel("Debug Renderer");

            UIBuilder.AddButton("Add Texture", () =>
            {
                textureGenerators.Add(new TextureGenerator(textureGenerators.Count, colorSlider, colorSlider, slider0, slider1, slider2, slider3));
                dirty = true;
            });

            UIBuilder.AddSlider("Pattern Direction", 0, 1, 0.5f, (val) =>
            {
                slider0 = val;
            });

            UIBuilder.AddSlider("Pattern Randomness", 0, 1, 0.5f, (val) =>
            {
                slider1 = val;
            });


            UIBuilder.AddSlider("Pattern Length", 0, 1, 0.5f, (val) =>
            {
                slider2 = val;
            });


            UIBuilder.AddSlider("Pattern Density", 0, 1, 0.5f, (val) =>
            {
                slider2 = val;
            });

            UIBuilder.AddColorSliderWithPreview((val) =>
            {
                colorSlider = val;
            });

            // Initialize Random with a fixed seed
            Random rng = new Random(0);

            // Generate 16 random texture generators
            for (int i = 0; i < 16; i++)
            {
                float randomSlider0 = (float)rng.NextDouble(); // Full range for direction
                float randomSlider1 = (float)rng.NextDouble() * 0.1f; // Limit randomness
                float randomSlider2 = 0.1f + (float)rng.NextDouble() * 0.3f; // Length range between 0.2 and 0.5
                float randomSlider3 = (float)rng.NextDouble() * 0.75f; // Full range for density

                // Fur-like color, adjust the hue for different color ranges
                double hue = rng.NextDouble(); // Hue range for natural fur colors
                Vec3 hsbColor = new Vec3(hue, 0.5 + rng.NextDouble() * 0.5, 0.5 + rng.NextDouble() * 0.5);
                Vec3 rgbColor = Vec3.HsbToRgb(hsbColor);
                Vec3 backgroundColor = Vec3.HsbToRgb(new Vec3(hsbColor.x, hsbColor.y, hsbColor.z - 0.2));

                textureGenerators.Add(new TextureGenerator(i, rgbColor, backgroundColor, randomSlider0, randomSlider1, randomSlider2, randomSlider3));
            }

            dirty = true;

            megaTexture = new GPUMegaTexture();
        }


        public void OnKeyPressed(Key key, ModifierKeys modifiers)
        {

        }

        public void OnLateRender(Renderer gpu)
        {

        }

        public void GenerateTextures(Renderer gpu, int width, int height)
        {
            megaTexture = new GPUMegaTexture();

            for(int i = 0; i < textureGenerators.Count; i++)
            {
                 using GPUImage texture = new GPUImage(width, height);

                gpu.ExecuteFilter(texture, new ClearWithColor(textureGenerators[i].backgroundColor));
                gpu.ExecuteFilter(texture, textureGenerators[i]);

                gpu.device.Synchronize();

                megaTexture.AddTexture(texture);
            }

            //megaTexture.SaveMegaTextureAsBitmap("output.png");
        }

        public void OnRender(Renderer gpu)
        {
            if(dirty)
            {
                GenerateTextures(gpu, 512, 512);
                dirty = false;
            }

            if(textureGenerators.Count > 0)
            {
                gpu.ExecuteMegaTextureMask<MegaTextureGrid>(gpu.framebuffer, megaTexture);
            }
            else
            {
                Thread.Sleep(10);
            }
        }

        public void OnStart(Renderer gpu)
        {

        }

        public void OnStop()
        {

        }

        public void SetMode(int mode)
        {

        }

        public (int xSize, int ySize, bool update) BeforeResolutionChanged(RenderWindow renderWindow, int newWidth, int newHeight)
        {
            return (newWidth, newHeight, false);
        }
    }

    public struct MegaTextureGrid : IMegaTextureMask
    {
        public RGBA32 Apply(int tick, float x, float y, dImage output, dMegaTexture input)
        {
            int textureCount = (int)input.TextureTickets.Length;
            int gridSize = (int)Math.Ceiling(Math.Sqrt(textureCount));

            // Calculate which texture to use based on x, y coordinates
            int gridX = (int)(x * gridSize);
            int gridY = (int)(y * gridSize);
            int textureID = Math.Min(gridY * gridSize + gridX, textureCount - 1); // Ensure textureID is within bounds

            // Calculate texture coordinates (normalized [0,1])
            float tx = (x * gridSize - gridX) * input.TextureTickets[textureID].Width;
            float ty = (y * gridSize - gridY) * input.TextureTickets[textureID].Height;

            // Sample color from the selected texture
            RGBA32 color = input.GetColorAt(textureID, (int)tx, (int)ty);

            return color;
        }
    }

    public struct ClearWithColor : IImageFilter
    {
        public Vec3 color;

        public ClearWithColor(Vec3 color)
        {
            this.color = color;
        }

        public RGBA32 Apply(int tick, float x, float y, dImage output)
        {
            return new RGBA32(color);
        }
    }

    public unsafe struct TextureGenerator : IImageFilter
    {
        public int count;

        public float slider0; // Can be used to adjust the direction of the strands
        public float slider1; // Can be used to adjust the randomness of the strands
        public float slider2; // Can be used to adjust the length of the strands
        public float slider3; // Can be used to adjust the density of the strands

        public Vec3 color;
        public Vec3 backgroundColor;

        public float globalDirection; // Controlled by sliders
        public float directionVariance; // Controlled by sliders
        fixed int permutation[256];

        public TextureGenerator(int count, Vec3 color, Vec3 backgroundColor, float slider0, float slider1, float slider2, float slider3)
        {
            this.count = count;
            this.color = color;
            this.backgroundColor = backgroundColor;
            this.slider0 = slider0;
            this.slider1 = slider1;
            this.slider2 = slider2;
            this.slider3 = slider3;

            // Convert sliders to global direction and variance
            globalDirection = slider0 * 2 * XMath.PI; // Map slider to a full circle
            directionVariance = slider1 * XMath.PI / 4; // Max deviation from the global direction

            int[] permSource = new int[]
            {
                151, 160, 137, 91, 90, 15, 131, 13, 201, 95, 96, 53, 194, 233, 7, 225, 140,
                36, 103, 30, 69, 142, 8, 99, 37, 240, 21, 10, 23, 190, 6, 148, 247, 120, 234,
                75, 0, 26, 197, 62, 94, 252, 219, 203, 117, 35, 11, 32, 57, 177, 33, 88, 237,
                149, 56, 87, 174, 20, 125, 136, 171, 168, 68, 175, 74, 165, 71, 134, 139, 48,
                27, 166, 77, 146, 158, 231, 83, 111, 229, 122, 60, 211, 133, 230, 220, 105, 92,
                41, 55, 46, 245, 40, 244, 102, 143, 54, 65, 25, 63, 161, 1, 216, 80, 73, 209,
                76, 132, 187, 208, 89, 18, 169, 200, 196, 135, 130, 116, 188, 159, 86, 164,
                100, 109, 198, 173, 186, 3, 64, 52, 217, 226, 250, 124, 123, 5, 202, 38, 147,
                118, 126, 255, 82, 85, 212, 207, 206, 59, 227, 47, 16, 58, 17, 182, 189, 28, 42,
                223, 183, 170, 213, 119, 248, 152, 2, 44, 154, 163, 70, 221, 153, 101, 155, 167,
                43, 172, 9, 129, 22, 39, 253, 19, 98, 108, 110, 79, 113, 224, 232, 178, 185, 112,
                104, 218, 246, 97, 228, 251, 34, 242, 193, 238, 210, 144, 12, 191, 179, 162, 241,
                81, 51, 145, 235, 249, 14, 239, 107, 49, 192, 214, 31, 181, 199, 106, 157, 184,
                84, 204, 176, 115, 121, 50, 45, 127, 4, 150, 254, 138, 236, 205, 93, 222, 114,
                67, 29, 24, 72, 243, 141, 128, 195, 78, 66, 215, 61, 156, 180
            };

            // Initialize the permutation table
            // Direct memory copy from permSource to permutation
            fixed (int* perm = permutation, source = permSource)
            {
                System.Buffer.MemoryCopy(source, perm, 256 * sizeof(int), 256 * sizeof(int));
            }
        }

        // Perlin noise helper functions
        private float Fade(float t)
        {
            return t * t * t * (t * (t * 6 - 15) + 10);
        }

        private float Lerp(float a, float b, float t)
        {
            return a + t * (b - a);
        }

        private float Grad(int hash, float x, float y, float z)
        {
            int h = hash & 15;
            float u = h < 8 ? x : y;
            float v = h < 4 ? y : h == 12 || h == 14 ? x : z;
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }

        // Function to get value from p array, generated on-the-fly
        private int GetP(int index)
        {
            fixed (int* perm = permutation)
            {
                return perm[index & 255];
            }
        }

        private float Perlin(float x, float y, float z)
        {
            int X = (int)XMath.Floor(x) & 255;
            int Y = (int)XMath.Floor(y) & 255;
            int Z = (int)XMath.Floor(z) & 255;
            x -= XMath.Floor(x);
            y -= XMath.Floor(y);
            z -= XMath.Floor(z);
            float u = Fade(x);
            float v = Fade(y);
            float w = Fade(z);

            int p0 = GetP(X) + Y;
            int p1 = GetP(X + 1) + Y;
            int p00 = GetP(p0) + Z;
            int p01 = GetP(p0 + 1) + Z;
            int p10 = GetP(p1) + Z;
            int p11 = GetP(p1 + 1) + Z;

            return Lerp(Lerp(Lerp(Grad(GetP(p00), x, y, z), Grad(GetP(p10), x - 1, y, z), u),
                   Lerp(Grad(GetP(p01), x, y - 1, z), Grad(GetP(p11), x - 1, y - 1, z), u), v),
                   Lerp(Lerp(Grad(GetP(p00 + 1), x, y, z - 1), Grad(GetP(p10 + 1), x - 1, y, z - 1), u),
                   Lerp(Grad(GetP(p01 + 1), x, y - 1, z - 1), Grad(GetP(p11 + 1), x - 1, y - 1, z - 1), u), v), w);
        }

        // this is called per pixel
        // x and y are in normalized space 0 - 1
        public RGBA32 Apply(int tick, float x, float y, dImage output)
        {
            // Generate a random value for density check
            float densityCheck = RandomValue(tick, x, y, count);

            // If the random value exceeds the density threshold, skip drawing the strand
            if (densityCheck < slider3)
            {
                // Calculate the strand direction
                float strandDirection = globalDirection + RandomDeviation(tick, x, y, count);
                float lengthInPixels = slider2 * Math.Min(output.width, output.height);

                Vec2 startPoint = new Vec2(x, y);
                Vec2 endPoint = new Vec2(
                    x + XMath.Cos(strandDirection) * lengthInPixels / output.width,
                    y + XMath.Sin(strandDirection) * lengthInPixels / output.height
                );

                // Draw the strand from startPoint to endPoint
                DrawStrand(output, startPoint, endPoint);
            }

            return new RGBA32(0);
        }

        float RandomValue(int tick, float x, float y, int count)
        {
            uint hash = (uint)(x * 12345.0 + y * 67890.0 + count * 13579.0 + tick * 24680.0);
            hash ^= hash << 13;
            hash ^= hash >> 17;
            hash ^= hash << 5;
            return (hash % 1000) / 1000.0f; // Normalized random value between 0 and 1
        }

        float RandomDeviation(int tick, float x, float y, int count)
        {
            // Simple hash function for pseudo-randomness
            uint hash = (uint)(x * 737.0 + y * 997.0 + count * 991.0 + tick * 877.0);
            hash ^= hash << 13;
            hash ^= hash >> 17;
            hash ^= hash << 5;
            // Normalize the result to get a value between -directionVariance and +directionVariance
            float normalizedHash = (hash % 1000) / 1000.0f;
            return (normalizedHash * 2 - 1) * directionVariance;
        }

        // Draw a strand from startPoint to endPoint
        void DrawStrand(dImage image, Vec2 startPoint, Vec2 endPoint)
        {
            // Convert normalized startPoint and endPoint to pixel space
            Vec2 pixelStart = new Vec2(startPoint.x * image.width, startPoint.y * image.height);
            Vec2 pixelEnd = new Vec2(endPoint.x * image.width, endPoint.y * image.height);

            Vec2 direction = pixelEnd - pixelStart;

            float stepSize = 0.25f;
            float length = direction.length() / stepSize;
            Vec2 step = Vec2.Normalize(direction) * stepSize;

            for (float i = 0; i <= length; i++)
            {
                Vec2 point = pixelStart + step * i;
                // Ensure the point is within the image boundaries
                if (point.x >= 0 && point.x < image.width && point.y >= 0 && point.y < image.height)
                {
                    RGBA32 existingColor = image.GetColorAt((int)point.x, (int)point.y);
                    RGBA32 c = new RGBA32(color);
                    c.a = (byte)(0.5 * 255f);
                    RGBA32 blendedColor = BlendColors(existingColor, c);
                    image.SetColorAt((int)point.x, (int)point.y, blendedColor);
                }
            }
        }


        // Blend two colors
        RGBA32 BlendColors(RGBA32 color1, RGBA32 color2)
        {
            // Convert byte values to floats for precise calculation
            float alpha1 = color1.a / 255.0f;
            float red1 = color1.r / 255.0f;
            float green1 = color1.g / 255.0f;
            float blue1 = color1.b / 255.0f;

            float alpha2 = color2.a / 255.0f;
            float red2 = color2.r / 255.0f;
            float green2 = color2.g / 255.0f;
            float blue2 = color2.b / 255.0f;

            // Perform alpha blending
            float alpha = alpha2 + alpha1 * (1 - alpha2);
            float red = (red2 * alpha2 + red1 * alpha1 * (1 - alpha2)) / alpha;
            float green = (green2 * alpha2 + green1 * alpha1 * (1 - alpha2)) / alpha;
            float blue = (blue2 * alpha2 + blue1 * alpha1 * (1 - alpha2)) / alpha;

            // Convert the float results back to bytes
            byte finalAlpha = (byte)(alpha * 255);
            byte finalRed = (byte)(red * 255);
            byte finalGreen = (byte)(green * 255);
            byte finalBlue = (byte)(blue * 255);

            return new RGBA32(finalRed, finalGreen, finalBlue, finalAlpha);
        }


    }
}
