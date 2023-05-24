using BadVideoStreaming.Comms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace BadVideoStreaming
{
    public class BiDirectionalStreaming : MessageHandler
    {
        public string address;
        public bool isServer;
        public Connection metaDataConnection;
        public VideoStreamingEndpoint receivedStream;
        public VideoStreamingOrigin sendingStream;
        public Action<int, int, int, long, byte[]> onNewFrame;
        public List<Action> onConnect;
        private string sendAddress;
        private string receiveAddress;

        public BiDirectionalStreaming(string address, bool isServer, Action<int, int, int, long, byte[]> onNewFrame, string receiveAddress, string sendAddress)
        {
            this.address = address;
            this.isServer = isServer;
            this.onNewFrame = onNewFrame;
            this.receiveAddress = receiveAddress;
            this.sendAddress = sendAddress;

            Trace.WriteLine($"local sendAddress: {sendAddress}");
            Trace.WriteLine($"local receiveAddress: {receiveAddress}");

            onConnect = new List<Action>();
        }

        public void Connect()
        {
            if (isServer)
            {
                this.metaDataConnection = new SocketServer(address, () =>
                {
                    metaDataConnection.Send(new Message { tag = GetTag(), message = $"init,{sendAddress},{receiveAddress}" }); ;
                });
                metaDataConnection.AddMessageHandler(this);
            }
            else
            {
                this.metaDataConnection = new SocketClient(address, () => { OnConnect(); });
                metaDataConnection.AddMessageHandler(this);
            }
        }

        public void SendFrame(byte streamID, Bitmap frame)
        {
            if (sendingStream != null)
            {
                sendingStream.SendFrame(new Frame(streamID, frame), sendAddress);
            }
        }


        public void SendFrame(byte streamID, int width, int height, int[] rgba32_image_data)
        {
            if (sendingStream != null)
            {
                // Create a new Bitmap object with the specified width and height
                Bitmap frame = new Bitmap(width, height, PixelFormat.Format32bppArgb);

                // Lock the bitmap's bits
                Rectangle rect = new Rectangle(0, 0, width, height);
                BitmapData bmpData = frame.LockBits(rect, ImageLockMode.WriteOnly, frame.PixelFormat);

                // Get the address of the first line
                IntPtr ptr = bmpData.Scan0;

                // Copy the RGB values into the bitmap
                int bytes = Math.Abs(bmpData.Stride) * frame.Height;
                byte[] rgbValues = new byte[bytes];
                Marshal.Copy(rgbValues, 0, ptr, bytes);

                // Convert the int array to a byte array
                Buffer.BlockCopy(rgba32_image_data, 0, rgbValues, 0, rgbValues.Length);

                // Copy the byte array back to the bitmap
                Marshal.Copy(rgbValues, 0, ptr, bytes);

                // Unlock the bits
                frame.UnlockBits(bmpData);

                sendingStream.SendFrame(new Frame(streamID, frame), sendAddress);
            }
        }


        private void OnConnect()
        {
            foreach(Action action in onConnect)
            {
                action();
            }
        }

        public string GetTag()
        {
            return "BiDirectionalStreaming";
        }

        public void OnStart(Connection connection) { } // unused

        public void Receive(Message message)
        {
            string[] split = message.message.Split(',');

            Trace.WriteLine(message.ToString());

            string command = split[0];

            switch (command)
            {
                case "init":
                    if (!isServer)
                    {
                        // Address where server sends data to
                        string serverSendAddress = split[1];
                        // Address where server receives data from
                        string serverReceiveAddress = split[2];

                        Trace.WriteLine($"server sendAddress: {serverSendAddress}");
                        Trace.WriteLine($"server receiveAddress: {serverReceiveAddress}");

                        // Initialize the VideoStreaming classes
                        this.receivedStream = new VideoStreamingEndpoint(receiveAddress, metaDataConnection, onNewFrame);
                        this.sendingStream = new VideoStreamingOrigin(sendAddress, metaDataConnection);

                        // Notify the server about client's address
                        metaDataConnection.Send(new Message { tag = GetTag(), message = $"ready,{sendAddress},{receiveAddress}" });
                    }
                    break;
                case "ready":
                    if (isServer)
                    {
                        // Address where client sends data to
                        string clientSendAddress = split[1];
                        // Address where client receives data from
                        string clientReceiveAddress = split[2];

                        Trace.WriteLine($"client sendAddress: {clientSendAddress}");
                        Trace.WriteLine($"client receiveAddress: {clientReceiveAddress}");

                        // Initialize the VideoStreaming classes
                        this.receivedStream = new VideoStreamingEndpoint(receiveAddress, metaDataConnection, onNewFrame);
                        this.sendingStream = new VideoStreamingOrigin(sendAddress, metaDataConnection);
                        OnConnect();
                    }
                    break;
                default: break;
            }
        }
    }

}
