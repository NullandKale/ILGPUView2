using BadVideoStreaming.Comms;
using System.Drawing;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace BadVideoStreaming
{
    public abstract class VideoConnection
    {
        public Action<int, int, int, long, byte[]>? onNewFrame;

        protected FrameTiming sentFrameTimer = new FrameTiming();
        protected FrameTiming receivedFrameTimer = new FrameTiming();
        public double TimePerFrameSentMS => sentFrameTimer.AverageTimePerFrameMs;
        public double TimePerFrameReceivedMS => receivedFrameTimer.AverageTimePerFrameMs;

        public abstract void SendFrame(byte streamID, Bitmap frame);
    }

    public class UdpVideoConnection : VideoConnection
    {
        private VideoStreamingEndpoint receivedStream;
        private VideoStreamingOrigin sendingStream;

        public UdpVideoConnection(string sendAddress, string receiveAddress, Connection metadataConnection, Action<int, int, int, long, byte[]> onNewFrame)
        {
            // Initialize the VideoStreaming classes here

            this.onNewFrame = (width, height, streamid, timestamp, imageData) =>
            {
                // time it here
                receivedFrameTimer.MarkFrameTime();

                // call the onNewFrame parameter
                onNewFrame(width, height, streamid, timestamp, imageData);
            };

            this.receivedStream = new VideoStreamingEndpoint(receiveAddress, metadataConnection, this.onNewFrame);
            this.sendingStream = new VideoStreamingOrigin(sendAddress, metadataConnection);
        }

        public override void SendFrame(byte streamID, Bitmap frame)
        {
            sentFrameTimer.MarkFrameTime();

            // Send the frame using UDP here
            sendingStream.SendFrame(new Frame(streamID, frame));
        }
    }

    public class TcpVideoConnection : VideoConnection, MessageHandler
    {
        private const string Tag = "Frame";
        private Connection metaDataConnection;

        public TcpVideoConnection(Connection metaDataConnection, Action<int, int, int, long, byte[]> onNewFrame)
        {
            this.metaDataConnection = metaDataConnection;

            this.onNewFrame = (width, height, streamid, timestamp, imageData) =>
            {
                // time it here
                receivedFrameTimer.MarkFrameTime();

                // call the onNewFrame parameter
                onNewFrame(width, height, streamid, timestamp, imageData);
            };

            metaDataConnection.AddMessageHandler(this);
        }

        public override void SendFrame(byte streamID, Bitmap frame)
        {
            sentFrameTimer.MarkFrameTime();
            Frame videoFrame = new Frame(streamID, frame);
            string base64Frame = Convert.ToBase64String(videoFrame.GetBytes().ToArray(), Base64FormattingOptions.None);
            metaDataConnection.Send(new Message { tag = Tag, message = base64Frame });
        }

        public void OnStart(Connection connection) { }

        public string GetTag() => Tag;

        public void Receive(Message message)
        {
            byte[] frameData = Convert.FromBase64String(message.message.Trim());
            Frame frame = new Frame(frameData);
            onNewFrame?.Invoke(frame.width, frame.height, frame.streamid, frame.timestamp, frame.imageData.ToArray());
        }
    }




}
