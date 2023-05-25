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
    public abstract class VideoConnection
    {
        public Action<int, int, int, long, byte[]> onNewFrame;

        public void SetOnNewFrameCallback(Action<int, int, int, long, byte[]> onNewFrame)
        {
            this.onNewFrame = onNewFrame;
        }

        public abstract void SendFrame(byte streamID, Bitmap frame);
    }

    public class UdpVideoConnection : VideoConnection
    {
        private VideoStreamingEndpoint receivedStream;
        private VideoStreamingOrigin sendingStream;

        public UdpVideoConnection(string sendAddress, string receiveAddress, Connection metadataConnection, Action<int, int, int, long, byte[]> onNewFrame)
        {
            // Initialize the VideoStreaming classes here
            this.receivedStream = new VideoStreamingEndpoint(receiveAddress, metadataConnection, onNewFrame);
            this.sendingStream = new VideoStreamingOrigin(sendAddress, metadataConnection);
        }

        public override void SendFrame(byte streamID, Bitmap frame)
        {
            // Send the frame using UDP here
            sendingStream.SendFrame(new Frame(streamID, frame));
        }
    }

    public class FrameMessageHandler : MessageHandler
    {
        private const string Tag = "Frame";
        private Action<byte[]> onFrameReceived;

        public FrameMessageHandler(Action<byte[]> onFrameReceived)
        {
            this.onFrameReceived = onFrameReceived;
        }

        public void OnStart(Connection connection) { }

        public string GetTag() => Tag;

        public void Receive(Message message)
        {
            byte[] frameData = Convert.FromBase64String(message.message.Trim());
            onFrameReceived(frameData);
        }
    }

    public class TcpVideoConnection : VideoConnection
    {
        private Connection metaDataConnection;
        private FrameMessageHandler frameHandler;

        public TcpVideoConnection(Connection metaDataConnection, Action<int, int, int, long, byte[]> onNewFrame)
        {
            this.metaDataConnection = metaDataConnection;

            frameHandler = new FrameMessageHandler(data =>
            {
                Frame frame = new Frame(data);
                onNewFrame(frame.width, frame.height, frame.streamid, frame.timestamp, frame.imageData.ToArray());
            });

            metaDataConnection.AddMessageHandler(frameHandler);
        }

        public override void SendFrame(byte streamID, Bitmap frame)
        {
            Frame videoFrame = new Frame(streamID, frame);

            string base64Frame = Convert.ToBase64String(videoFrame.GetBytes().ToArray(), Base64FormattingOptions.None);

            metaDataConnection.Send(new Message { tag = frameHandler.GetTag(), message = base64Frame });
        }
    }



    public class BiDirectionalStreaming : MessageHandler
    {
        public string address;
        public bool isServer;
        public VideoConnection videoConnection;
        public Connection metaDataConnection;
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
            if (videoConnection != null)
            {
                videoConnection.SendFrame(streamID, frame);
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

                        //server sendAddress: 69.125.155.57:5001
                        //server receiveAddress: 73.213.36.216:5000
                        Trace.WriteLine($"server sendAddress: {serverSendAddress}");
                        Trace.WriteLine($"server receiveAddress: {serverReceiveAddress}");

                        this.videoConnection = new TcpVideoConnection(metaDataConnection, onNewFrame);
                        //this.videoConnection = new UdpVideoConnection(sendAddress, receiveAddress, metaDataConnection, onNewFrame);

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

                        //client sendAddress: 73.213.36.216:5001
                        //client receiveAddress: 69.125.155.57:5000
                        Trace.WriteLine($"client sendAddress: {clientSendAddress}");
                        Trace.WriteLine($"client receiveAddress: {clientReceiveAddress}");

                        this.videoConnection = new TcpVideoConnection(metaDataConnection, onNewFrame);
                        //this.videoConnection = new UdpVideoConnection(sendAddress, receiveAddress, metaDataConnection, onNewFrame);

                        OnConnect();
                    }
                    break;
                default: break;
            }
        }
    }

}
