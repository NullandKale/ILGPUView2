using GPU;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static GPU.Kernels;
using System.Windows.Input;
using UIElement;
using BadVideoStreaming;
using System.Drawing;
using ILGPUView2.GPU.Filters;
using System.Threading;
using System.IO;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace ExampleProject.Modes
{
    public class VideoStreamingMode : IRenderCallback
    {
        private string bitmapFolder = "C:\\Users\\alec\\source\\python\\depth_video\\input";
        private string localExternalIP = "73.213.36.216";
        private string remoteIP = "192.168.0.195";
        private bool isServer = true;

        private BiDirectionalStreaming biDirectionalStreaming;

        private GPUImage frame;

        public void CreateUI()
        {
            UIBuilder.Clear();
            UIBuilder.AddLabel("Video Streaming Mode");

            UIBuilder.AddLabel($"Image Set Folder: ");
            UIBuilder.AddTextBox(bitmapFolder, (newVal) => { bitmapFolder = newVal; });

            localExternalIP = BadVideoStreaming.Comms.Utils.GetLocalIPAddress();
            UIBuilder.AddLabel($"Self IP");
            UIBuilder.AddTextBox(localExternalIP, (newVal) => { localExternalIP = newVal; });

            UIBuilder.AddLabel("Remote IP");
            UIBuilder.AddTextBox(remoteIP, (newVal) => { remoteIP = newVal; });

            string[] modes = { "server", "client" };
            UIBuilder.AddDropdown(modes, (newVal) => { isServer = newVal == 0; });

            UIBuilder.AddButton("Connect", () =>
            {
                string address = remoteIP + ":4010";
                string receiveAddress = isServer ? $"{localExternalIP}:5000" : $"{localExternalIP}:5001";
                string sendAddress = isServer ? $"{remoteIP}:5001" : $"{remoteIP}:5000";

                biDirectionalStreaming = new BiDirectionalStreaming(address, isServer, OnNewFrame, receiveAddress, sendAddress);
                biDirectionalStreaming.onConnect.Add(() =>
                {
                    Thread t = new Thread(() =>
                    {
                        // Get all files in the bitmapFolder directory
                        string[] files = Directory.GetFiles(bitmapFolder);
                        while(true)
                        {
                            for (int i = 0; i < files.Length; i++)
                            {
                                string? file = files[isServer ? i : (files.Length - 1) - i];
                                // Only process image files
                                if (file.EndsWith(".jpg") || file.EndsWith(".jpeg") || file.EndsWith(".png"))
                                {
                                    // Load the image file into a Bitmap
                                    using var bitmap = new Bitmap(file);

                                    // Send the bitmap
                                    biDirectionalStreaming.SendFrame(0, bitmap);

                                    // Wait for 33ms (roughly 30 FPS)
                                    Thread.Sleep(33);
                                }
                            }
                        }
                    });
                    t.IsBackground = true;
                    t.Start();
                });
                biDirectionalStreaming.Connect();
            });
        }

        public void OnNewFrame(int streamid, int width, int height, long timestamp, byte[] bytes)
        {
            try
            {
                using var ms = new MemoryStream(bytes);
                using var bitmap = new Bitmap(ms);
                frame = new GPUImage(bitmap);
            }
            catch(Exception e)
            {
                Trace.WriteLine(e);
            }
        }


        public void OnKeyPressed(Key key, ModifierKeys modifiers)
        {

        }

        public void OnLateRender(Device gpu)
        {

        }

        public void OnRender(Device gpu)
        {
            if(frame != null)
            {
                gpu.ExecuteMask<Scale>(gpu.framebuffer, frame);
            }
            else
            {
                Thread.Sleep(33);
            }
        }

        public void OnStart(Device gpu)
        {

        }

        public void OnStop()
        {

        }

        public void SetMode(int mode)
        {

        }

        public (int xSize, int ySize, bool update) BeforeResolutionChanged(RenderWindow renderWindow, int newWidth, int newHeight)
        {
            return (newWidth, newHeight, false);
        }
    }
}
