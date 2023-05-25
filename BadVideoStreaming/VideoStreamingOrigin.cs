using BadVideoStreaming.Comms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace BadVideoStreaming
{
    public class VideoStreamingOrigin
    {
        public Connection metadataConnection;
        public int port;
        private UdpClient udpClient;

        public VideoStreamingOrigin(string udpAddress, Connection metadataConnection)
        {
            this.metadataConnection = metadataConnection;
            port = int.Parse(udpAddress.Split(':')[1]);

            // Create a UdpClient for sending data
            udpClient = new UdpClient(port);
        }

        public void SendFrame(Frame frame, string destinationAddress)
        {
            var addressParts = destinationAddress.Split(':');
            var destination = new IPEndPoint(IPAddress.Parse(addressParts[0]), int.Parse(addressParts[1]));

            // Send the UDP data
            Span<byte> buffer = frame.GetBytes();
            udpClient.Send(buffer.ToArray(), buffer.Length, destination);
        }
    }
}
