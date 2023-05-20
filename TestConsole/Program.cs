using BadVideoStreaming;
using BadVideoStreaming.Comms;
using System;
using System.Drawing;

namespace BiDirectionalStreamingTest
{
    class Program
    {
        static void Main(string[] args)
        {
            // Set up server
            var serverAddress = "127.0.0.1:4010";
            var udpSendAddress = "127.0.0.1:5000";
            var udpReceiveAddress = "127.0.0.1:5001";
            var server = new BiDirectionalStreaming(serverAddress, isServer: true, null, udpSendAddress, udpReceiveAddress);

            // Set up client
            var clientUdpSendAddress = "127.0.0.1:6000";
            var clientUdpReceiveAddress = "127.0.0.1:6001";
            var client = new BiDirectionalStreaming(serverAddress, isServer: false, null, clientUdpSendAddress, clientUdpReceiveAddress);

            // The connect message is now handled internally in the BiDirectionalStreaming class

            // Wait for user input before closing
            Console.ReadKey();
        }
    }
}
