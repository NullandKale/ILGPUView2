using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace BadVideoStreaming.Comms
{
    public static class Utils
    {
        public static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());

            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }

            throw new Exception("Local IP Address Not Found!");
        }
    }

    public class FrameTiming
    {
        private DateTime? lastFrameTime;
        private List<double> frameIntervals = new List<double>();

        public double AverageTimePerFrameMs
        {
            get
            {
                if (frameIntervals.Count == 0) return 0;
                return frameIntervals.Average();
            }
        }
        public void MarkFrameTime()
        {
            DateTime now = DateTime.UtcNow;
            if (lastFrameTime.HasValue)
            {
                TimeSpan interval = now - lastFrameTime.Value;
                frameIntervals.Add(interval.TotalMilliseconds);
                if (frameIntervals.Count > 100) // Keep the last 100 samples
                {
                    frameIntervals.RemoveAt(0);
                }
            }
            lastFrameTime = now;
        }
    }
}
