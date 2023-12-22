using GPU;
using ILGPU.Runtime;
using ILGPU;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace ILGPUView2.GPU.DataStructures
{
    public struct MegaTextureScale : IMegaTextureMask
    {
        int textureID; 

        public MegaTextureScale(int textureID)
        {
            this.textureID = textureID;
        }

        public RGBA32 Apply(int tick, float x, float y, dImage output, dMegaTexture input)
        {
            return new RGBA32(input.GetPixel(textureID, x, y));
        }
    }



    public struct dTextureTicket
    {
        public int StartIndex;
        public int TextureSize;
        public int TextureID;
        public int Width;
        public int Height;

        public dTextureTicket(int startIndex, int textureSize, int textureID, int width, int height)
        {
            StartIndex = startIndex;
            TextureSize = textureSize;
            TextureID = textureID;
            Width = width;
            Height = height;
        }
    }

    public class GPUMegaTexture
    {
        public int count = 0;
        private List<dTextureTicket> textureTickets;
        private List<int> textureData;
        private MemoryBuffer1D<int, Stride1D.Dense> GPUTextureData;
        private MemoryBuffer1D<dTextureTicket, Stride1D.Dense> GPUTextureTickets;

        private bool isTextureDataDirty;

        public GPUMegaTexture()
        {
            textureTickets = new List<dTextureTicket>();
            textureData = new List<int>(); // Initialize the texture data list
        }


        public int AddTexture(GPUImage image)
        {
            count++;

            int startIndex = textureData?.Count ?? 0;
            int textureSize = image.toCPU().Length;
            int textureID = textureTickets.Count;

            // Include image dimensions in the texture ticket
            dTextureTicket ticket = new dTextureTicket(startIndex, textureSize, textureID, image.width, image.height);
            textureTickets.Add(ticket);

            UpdateTextureData(image.toCPU(), startIndex);

            isTextureDataDirty = true;
            return textureID;
        }

        private void UpdateTextureData(int[] newTextureData, int startIndex)
        {
            if (textureData.Capacity < startIndex + newTextureData.Length)
            {
                textureData.Capacity = startIndex + newTextureData.Length;
            }
            textureData.AddRange(newTextureData);
        }


        public dMegaTexture ToGPU(Renderer gpu)
        {
            if (isTextureDataDirty || GPUTextureData == null)
            {
                GPUTextureData?.Dispose();
                GPUTextureData = gpu.device.Allocate1D(textureData.ToArray());

                GPUTextureTickets?.Dispose();
                GPUTextureTickets = gpu.device.Allocate1D(textureTickets.ToArray());

                isTextureDataDirty = false;
            }

            return new dMegaTexture(GPUTextureData.View, GPUTextureTickets.View);
        }

        public void SaveMegaTextureAsBitmap(string filePath)
        {
            int textureCount = textureTickets.Count;
            int gridSize = (int)Math.Ceiling(Math.Sqrt(textureCount));

            int singleTextureWidth = textureTickets[0].Width;
            int singleTextureHeight = textureTickets[0].Height;

            int bitmapWidth = gridSize * singleTextureWidth;
            int bitmapHeight = gridSize * singleTextureHeight;

            using (Bitmap megaBitmap = new Bitmap(bitmapWidth, bitmapHeight))
            {
                using (Graphics g = Graphics.FromImage(megaBitmap))
                {
                    for (int i = 0; i < textureCount; i++)
                    {
                        int gridX = i % gridSize;
                        int gridY = i / gridSize;

                        int drawX = gridX * singleTextureWidth;
                        int drawY = gridY * singleTextureHeight;

                        dTextureTicket ticket = textureTickets[i];
                        int[] textureData = GetTextureData(ticket);
                        Bitmap textureBitmap = Utils.BitmapFromBytes(textureData, ticket.Width, ticket.Height);

                        g.DrawImage(textureBitmap, new Rectangle(drawX, drawY, singleTextureWidth, singleTextureHeight));
                        textureBitmap.Dispose();
                    }
                }

                megaBitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
            }
        }


        public int[] GetTextureData(dTextureTicket ticket)
        {
            int[] textureData = new int[ticket.TextureSize];
            Array.Copy(this.textureData.ToArray(), ticket.StartIndex, textureData, 0, ticket.TextureSize);
            return textureData;
        }

    }

    public struct dMegaTexture
    {
        public ArrayView1D<int, Stride1D.Dense> Textures;
        public ArrayView1D<dTextureTicket, Stride1D.Dense> TextureTickets;

        public dMegaTexture(ArrayView1D<int, Stride1D.Dense> textures, ArrayView1D<dTextureTicket, Stride1D.Dense> textureTickets)
        {
            Textures = textures;
            TextureTickets = textureTickets;
        }

        private int GetIndex(int textureID, int x, int y)
        {
            var ticket = TextureTickets[textureID];
            return y * ticket.Width + x + ticket.StartIndex;
        }

        public RGBA32 GetColorAt(int textureID, int x, int y)
        {
            var ticket = TextureTickets[textureID];
            if (x < 0 || x >= ticket.Width || y < 0 || y >= ticket.Height)
            {
                return new RGBA32();
            }

            int index = GetIndex(textureID, x, y);
            return new RGBA32(Textures[index]);
        }

        public void SetColorAt(int textureID, int x, int y, int color)
        {
            var ticket = TextureTickets[textureID];
            if (x < 0 || x >= ticket.Width || y < 0 || y >= ticket.Height)
            {
                return;
            }

            int index = GetIndex(textureID, x, y);
            Textures[index] = color;
        }

        public void SetColorAt(int textureID, int x, int y, RGBA32 color)
        {
            SetColorAt(textureID, x, y, color.ToInt());
        }

        public RGBA32 GetColorAt(int textureID, float x, float y)
        {
            var ticket = TextureTickets[textureID];
            int x_idx = (int)(x * ticket.Width);
            int y_idx = (int)(y * ticket.Height);

            return GetColorAt(textureID, x_idx, y_idx);
        }

        public Vec3 GetPixel(int textureID, float x, float y)
        {
            return GetColorAt(textureID, x, y).toVec3();
        }
    }


}
