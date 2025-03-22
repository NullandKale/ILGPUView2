using GPU;
using ILGPU.Runtime;
using Microsoft.Win32;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Input;
using UIElement;
using ILGPUView2.GPU.DataStructures;
using System.Formats.Tar;
using ILGPUView2.GPU.Filters;
using System.Windows.Controls;
using System.Windows;
using System.Diagnostics;

namespace ExampleProject.Modes
{
    public partial class VideoPlaybackTestMode : IRenderCallback
    {
        public GPUImage chosenTexture;
        public Label fpsLabel;
        public void CreateUI()
        {
            fpsLabel = UIBuilder.AddLabel("Video Playback FPS");

            UIBuilder.AddFilePicker("Pick a Video)", "", "", (filename) =>
            {
                if(chosenTexture != null)
                {
                    chosenTexture.Dispose();
                }
                chosenTexture = CreateGPUImageOrVideo(filename);
            });

        }

        private GPUImage CreateGPUImageOrVideo(string filename)
        {
            try
            {
                // Attempt GPUVideoImage
                var testVideo = new GPUVideoImage(filename);
                return testVideo;
            }
            catch
            {
                // If it failed, fallback to still image
                if (GPUImage.TryLoad(filename, out GPUImage fallback))
                {
                    return fallback;
                }
                else
                {
                    Console.WriteLine($"Could not open {filename} as video or image.");
                    return null;
                }
            }
        }


        public void OnRender(Renderer gpu)
        {
            // If chosenTexture is a GPUVideoImage, pop the frame
            if (chosenTexture is GPUVideoImage cv)
            {
                cv.PopFrame(gpu);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    fpsLabel.Content = "Video Playback FPS: " + cv.videoReader.AveragePlaybackFps;
                });

            }

            gpu.ExecuteMask(gpu.framebuffer, chosenTexture != null ? chosenTexture : gpu.framebuffer, new Scale());
        }

        public void OnLateRender(Renderer gpu)
        {
        }

        public void OnKeyPressed(Key key, ModifierKeys modifiers)
        {
        }

        public void OnStart(Renderer gpu)
        {
        }

        public void OnStop()
        {
        }

        public void SetMode(int mode)
        {
            // not used in this test
        }

        public (int xSize, int ySize, bool update) BeforeResolutionChanged(RenderWindow renderWindow, int newWidth, int newHeight)
        {
            return (newWidth, newHeight, false);
        }
    }
}
