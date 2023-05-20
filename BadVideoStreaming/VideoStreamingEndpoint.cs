using BadVideoStreaming.Comms;
using System.Net.Sockets;
using System.Net;
using System.Drawing;

namespace BadVideoStreaming
{
    public class VideoStreamingEndpoint
    {
        public Connection metadataConnection;
        public string address;
        private UdpClient udpClient;
        private Action<int, int, int, long, byte[]> onNewFrame;

        public VideoStreamingEndpoint(string udpAddress, Connection metadataConnection, Action<int, int, int, long, byte[]> onNewFrame)
        {
            address = udpAddress;
            this.metadataConnection = metadataConnection;
            this.onNewFrame = onNewFrame;

            // Create a UdpClient for receiving data
            var udpAddressParts = udpAddress.Split(':');
            udpClient = new UdpClient(int.Parse(udpAddressParts[1]));

            // Start listening for incoming data
            udpClient.BeginReceive(ReceiveUdpData, null);
        }


        private void ReceiveUdpData(IAsyncResult res)
        {
            IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
            byte[] received = udpClient.EndReceive(res, ref RemoteIpEndPoint);

            // Process the received UDP data
            HandleReceivedUdpData(received);

            // Continue listening for UDP data
            udpClient.BeginReceive(new AsyncCallback(ReceiveUdpData), null);
        }

        private void HandleReceivedUdpData(byte[] data)
        {
            // TODO: Check that data is long enough to contain a Frame header
            Frame receivedFrame = new Frame(data);
            onNewFrame(receivedFrame.streamid, receivedFrame.width, receivedFrame.height, receivedFrame.timestamp, receivedFrame.tobytes());
        }
    }

}
