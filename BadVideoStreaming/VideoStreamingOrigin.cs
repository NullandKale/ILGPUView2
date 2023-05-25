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
        public IPEndPoint destination;
        public string address;
        public int port;
        private UdpClient udpClient;

        public VideoStreamingOrigin(string udpAddress, Connection metadataConnection)
        {
            this.metadataConnection = metadataConnection;

            var addressParts = udpAddress.Split(':');
            address = addressParts[0];
            port = int.Parse(addressParts[1]);
            destination = new IPEndPoint(IPAddress.Parse(address), port);

            // Create a UdpClient for sending data
            udpClient = new UdpClient(port);
        }

        public void SendFrame(Frame frame)
        {
            // Send the UDP data
            Span<byte> buffer = frame.GetBytes();
            udpClient.Send(buffer.ToArray(), buffer.Length, destination);
        }
    }
}
