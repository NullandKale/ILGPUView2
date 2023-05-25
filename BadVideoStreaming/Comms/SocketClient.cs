using System.Net.Sockets;
using System.Text;

namespace BadVideoStreaming.Comms
{
    public class SocketClient : Connection
    {
        private Thread listenThread;

        public SocketClient(string address, Action onConnected) : base(address, onConnected)
        {
            var splitAddress = address.Split(':');
            tcpClient = new TcpClient(splitAddress[0], int.Parse(splitAddress[1]));
            stream = tcpClient.GetStream();

            listenThread = new Thread(new ThreadStart(ListenForServer));
            listenThread.IsBackground = true;
            listenThread.Start();
        }

        private void ListenForServer()
        {
            bool connected = false;

            var message = new StringBuilder();
            int bytesRead;
            var buffer = new byte[1024];

            while (true)
            {
                bytesRead = 0;

                try
                {
                    bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (!connected)
                    {
                        connected = true;
                        onConnected();
                    }
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

                if (message.ToString().EndsWith("\n"))
                {
                    HandleMessage(message.ToString().Trim());
                    message.Clear();
                }
            }

            tcpClient.Close();
        }

        public override void Send(Message message)
        {
            if (stream != null)
            {
                byte[] msg = Encoding.ASCII.GetBytes($"{message.ToString()}\n");
                stream.Write(msg, 0, msg.Length);
            }
        }

    }
}

