using System.Drawing;
using System.IO;
using System;
using System.Drawing.Imaging;

namespace BadVideoStreaming
{
    public ref struct Frame
    {
        public byte streamid = 0;
        public ushort width = 0;
        public ushort height = 0;
        public long timestamp = 0;
        public Span<byte> imageData;

        public Frame(byte streamid, Bitmap image)
        {
            this.streamid = streamid;
            this.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Convert the image to a byte array
            using Bitmap resizedFrame = new Bitmap(image, new Size((int)(1280), (int)(720)));
            using var ms = new MemoryStream();

            var jpgEncoder = GetEncoder(ImageFormat.Jpeg);
            var myEncoderParameters = new EncoderParameters(1);
            myEncoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, 50L); // Quality can be adjusted here

            resizedFrame.Save(ms, jpgEncoder, myEncoderParameters);
            this.imageData = new Span<byte>(ms.ToArray());

            this.width = (ushort)resizedFrame.Width;
            this.height = (ushort)resizedFrame.Height;
        }

        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();

            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }


        public Frame(byte[] data)
        {
            // TODO: Check that data is long enough to contain a Frame header

            // Parse the frame header
            this.streamid = data[0];
            this.width = BitConverter.ToUInt16(data, 1);
            this.height = BitConverter.ToUInt16(data, 3);
            this.timestamp = BitConverter.ToInt64(data, 5);

            // Get the image data
            byte[] imageData = new byte[data.Length - 13];
            Array.Copy(data, 13, imageData, 0, imageData.Length);

            this.imageData = new Span<byte>(imageData);
        }

        // returns the bytes to be send via udp to the VideoStreamingEndpoint 
        public Span<byte> GetBytes()
        {
            byte[] header = new byte[13];

            header[0] = this.streamid;
            Array.Copy(BitConverter.GetBytes(this.width), 0, header, 1, 2);
            Array.Copy(BitConverter.GetBytes(this.height), 0, header, 3, 2);
            Array.Copy(BitConverter.GetBytes(this.timestamp), 0, header, 5, 8);

            byte[] combined = new byte[header.Length + this.imageData.Length];
            Array.Copy(header, 0, combined, 0, header.Length);
            Array.Copy(this.imageData.ToArray(), 0, combined, header.Length, this.imageData.Length);

            return new Span<byte>(combined);
        }

        public byte[] tobytes()
        {
            return imageData.ToArray();
        }
    }
}
