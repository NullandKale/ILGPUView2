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

            byte[] message = new byte[4096];
            int bytesRead;

            while (true)
            {
                bytesRead = 0;

                try
                {
                    bytesRead = stream.Read(message, 0, 4096);
                    if(!connected)
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

