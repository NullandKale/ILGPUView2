using System.Net.Sockets;
using System.Net;
using System.Text;

namespace BadVideoStreaming.Comms
{
    public class SocketServer : Connection
    {
        private TcpListener server;
        private Thread listenThread;

        public SocketServer(string address, Action onConnected) : base(address, onConnected)
        {
            var splitAddress = address.Split(':');
            server = new TcpListener(IPAddress.Parse(splitAddress[0]), int.Parse(splitAddress[1]));
            server.Start();

            listenThread = new Thread(new ThreadStart(ListenForClients));
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

            byte[] message = new byte[4096];
            int bytesRead;

            while (true)
            {
                bytesRead = 0;

                try
                {
                    bytesRead = stream.Read(message, 0, 4096);
                }
                catch
                {
                    break;
                }

                if (bytesRead == 0)
                {
                    break;
                }

                var incomingMessage = Encoding.ASCII.GetString(message, 0, bytesRead);
                HandleMessage(incomingMessage);
            }

            tcpClient.Close();
        }

        public override void Send(Message message)
        {
            if (stream != null)
            {
                byte[] msg = Encoding.ASCII.GetBytes(message.ToString());
                stream.Write(msg, 0, msg.Length);
            }
        }
    }
}

