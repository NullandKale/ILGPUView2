using System.Net.Sockets;
using System.Net;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Reflection.Metadata;
using System.Reflection;
using System.Threading;
using System;

namespace BadVideoStreaming.Comms
{
    public class SocketServer : Connection
    {
        private TcpListener server;
        private Thread listenThread;

        public SocketServer(string address, Action onConnected) : base(address, onConnected)
        {
            var splitAddress = address.Split(':');
            server = new TcpListener(IPAddress.Any, int.Parse(splitAddress[1]));

            server.Start();

            listenThread = new Thread(new ThreadStart(ListenForClients));
            listenThread.IsBackground = true;
            listenThread.Start();
        }

        private void ListenForClients()
        {
            while (true)
            {
                //blocks until a client has connected to the server
                TcpClient client = this.server.AcceptTcpClient();

                //create a new thread to handle communication
                Thread clientThread = new Thread(new ParameterizedThreadStart(HandleClientComm));
                clientThread.IsBackground = true;
                clientThread.Start(client);
            }
        }

        private void HandleClientComm(object client)
        {
            tcpClient = (TcpClient)client;
            stream = tcpClient.GetStream();
            onConnected();

            var message = new StringBuilder();
            int bytesRead;
            var buffer = new byte[1024];

            while (true)
            {
                bytesRead = 0;

                try
                {
                    bytesRead = stream.Read(buffer, 0, buffer.Length);
                }
                catch
                {
                    break;
                }

                if (bytesRead == 0)
                {
                    break;
                }

                message.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));

                // Split the received data by newline characters
                string[] messages = message.ToString().Split('\n');

                for (int i = 0; i < messages.Length - 1; i++)
                {
                    // Handle each complete message
                    HandleMessage(messages[i]);
                }

                // Keep any incomplete message for the next read
                message = new StringBuilder(messages[messages.Length - 1]);
            }

            tcpClient.Close();
        }



        public override void Send(Message message)
        {
            if (stream != null)
            {
                byte[] msg = Encoding.ASCII.GetBytes($"{message.ToString()}\n");
                stream.Write(msg, 0, msg.Length);
                stream.Flush();
            }
        }

    }
}

