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
