using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace BadVideoStreaming.Comms
{
    public class Message
    {
        public string tag;
        public string message;

        public override string ToString()
        {
            return tag + " " + message;
        }
    }

    public interface MessageHandler
    {
        public void OnStart(Connection connection);
        public string GetTag();
        public void Receive(Message message);
    }

    public abstract class Connection
    {
        public string address;
        protected TcpClient tcpClient;
        protected NetworkStream stream;
        protected Dictionary<string, MessageHandler> messageHandlers = new Dictionary<string, MessageHandler>();
        protected Action onConnected;
        public Connection(string address, Action onConnected)
        {
            this.address = address;
            this.onConnected = onConnected;
        }

        public abstract void Send(Message message);

        public void AddMessageHandler(MessageHandler handler)
        {
            messageHandlers[handler.GetTag()] = handler;
        }

        protected void HandleMessage(string message)
        {
            string[] split = message.Split(' ');
            if(split.Length == 2)
            {
                if (messageHandlers.TryGetValue(split[0], out MessageHandler handler))
                {
                    handler.Receive(new Message { tag = split[0], message = split[1] });
                }
            }
            else
            {
                Console.WriteLine("Failed to parse: " + message);
            }
        }
    }
}
