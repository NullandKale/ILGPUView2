using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using OpenCvSharp;
using GPU;

namespace ILGPUView2.GPU.DataStructures
{
    /// <summary>
    /// Asynchronously reads frames from a video file using OpenCV. Stores frames in 
    /// BGRA (8UC4) format and tracks the average playback FPS.
    /// </summary>
    public class AsyncVideoReader : IDisposable
    {
        private readonly VideoCapture capture;
        private readonly Thread frameReadThread;
        private volatile bool isRunning;
        private readonly double frameIntervalMs;

        // We'll store frames in a double buffer of BGRA mats:
        private readonly Mat[] frameMats = new Mat[2];

        // Temporary BGR mat for reading
        private readonly Mat bgrMat;

        // Double buffer index
        private int currentBufferIndex = 0;
        private readonly object bufferLock = new object();

        // Frame counting and timing
        private volatile int framesRead = 0; // increment each time we read a frame successfully
        private readonly Stopwatch playbackStopwatch;

        public string VideoFile { get; }
        public int Width { get; }
        public int Height { get; }
        public double Fps { get; }

        /// <summary>
        /// Returns the average playback FPS based on how many frames have been read 
        /// and how much time has elapsed in the background thread since this reader started.
        /// </summary>
        public double AveragePlaybackFps
        {
            get
            {
                double elapsed = playbackStopwatch.Elapsed.TotalSeconds;
                if (elapsed < 0.0001) // avoid divide-by-zero
                    return 0.0;
                return framesRead / elapsed;
            }
        }

        public AsyncVideoReader(string videoFile)
        {
            VideoFile = videoFile;

            capture = new VideoCapture(videoFile, VideoCaptureAPIs.ANY);

            if (!capture.IsOpened())
                throw new ArgumentException($"Could not open video file: {videoFile}");

            Width = capture.FrameWidth;
            Height = capture.FrameHeight;
            Fps = capture.Fps;
            frameIntervalMs = 1000.0 / Fps;

            // Prepare mats: one BGR for reading, two BGRA for double buffering
            bgrMat = new Mat(Height, Width, MatType.CV_8UC3);
            frameMats[0] = new Mat(Height, Width, MatType.CV_8UC4);
            frameMats[1] = new Mat(Height, Width, MatType.CV_8UC4);

            // Start timing
            playbackStopwatch = new Stopwatch();
            playbackStopwatch.Start();

            // Start the background frame reading thread
            isRunning = true;
            frameReadThread = new Thread(FrameReadLoop)
            {
                IsBackground = true
            };
            frameReadThread.Start();
        }

        private void FrameReadLoop()
        {
            var timer = Stopwatch.StartNew();
            double nextFrameTime = 0.0;

            while (isRunning)
            {
                try
                {
                    double currentTime = timer.Elapsed.TotalMilliseconds;

                    if (currentTime >= nextFrameTime)
                    {
                        int nextBufferIndex = 1 - currentBufferIndex;
                        Mat targetMat = frameMats[nextBufferIndex];

                        // 1) read raw BGR frame
                        bool frameRead = capture.Read(bgrMat);
                        if (!frameRead)
                        {
                            // if at the end, loop
                            capture.PosFrames = 0;
                            frameRead = capture.Read(bgrMat);
                        }

                        // 2) convert BGR -> BGRA
                        if (frameRead)
                        {
                            Cv2.CvtColor(bgrMat, targetMat, ColorConversionCodes.BGR2RGBA);
                            lock (bufferLock)
                            {
                                currentBufferIndex = nextBufferIndex;
                            }
                            framesRead++;

                            // schedule next read time
                            nextFrameTime = currentTime + frameIntervalMs;
                        }
                    }

                    // Calculate how long we have until the next frame time
                    double sleepTime = nextFrameTime - timer.Elapsed.TotalMilliseconds;

                    if (sleepTime > 2.0)
                    {
                        // If we have more than ~2 ms, do a loop of short sleeps
                        // This avoids overshooting by a large margin.
                        while (timer.Elapsed.TotalMilliseconds < (nextFrameTime - 1.0) && isRunning)
                        {
                            Thread.Sleep(1);
                        }
                    }

                    // For whatever remains (≤2 ms), spin-wait until we hit nextFrameTime or exit
                    while ((timer.Elapsed.TotalMilliseconds < nextFrameTime) && isRunning)
                    {
                        Thread.Yield();
                    }
                }
                catch (ThreadAbortException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Frame read error: {ex.Message}");
                    isRunning = false;
                }
            }
        }


        /// <summary>
        /// Returns a pointer to the latest BGRA frame data (MatType = CV_8UC4).
        /// On a little-endian system, the bytes in memory for each pixel are [B, G, R, A],
        /// which correspond to 0xAARRGGBB if interpreted as a 32-bit int.
        /// </summary>
        public IntPtr GetCurrentFramePtr()
        {
            lock (bufferLock)
            {
                return frameMats[currentBufferIndex].Data;
            }
        }

        public void Dispose()
        {
            isRunning = false;

            if (frameReadThread != null && frameReadThread.IsAlive)
            {
                // Wait for the thread to exit
                frameReadThread.Join(1000);
                if (frameReadThread.IsAlive)
                    frameReadThread.Abort();
            }

            capture?.Dispose();
            bgrMat?.Dispose();
            frameMats[0]?.Dispose();
            frameMats[1]?.Dispose();
        }
    }

    /// <summary>
    /// A GPUImage subclass that uses AsyncVideoReader to grab frames in BGRA and
    /// copies them into its int[] buffer (AARRGGBB). We do a single Marshal.Copy 
    /// to move the entire frame at once, then mark the CPU data as dirty.
    /// </summary>
    public class GPUVideoImage : GPUImage
    {
        public readonly AsyncVideoReader videoReader;

        public GPUVideoImage(string videoFile)
            : base(1, 1) // We'll adjust width/height after opening video
        {
            videoReader = new AsyncVideoReader(videoFile);

            // Update parent class's width & height
            this.width = videoReader.Width;
            this.height = videoReader.Height;

            // Allocate parent class's CPU buffer
            this.data = new int[width * height];
        }

        /// <summary>
        /// Copies the latest BGRA frame into 'data' (AARRGGBB). Single 
        /// Marshal.Copy for max efficiency, then sets cpu_dirty = true.
        /// </summary>
        public void PopFrame(Renderer gpu)
        {
            IntPtr ptr = videoReader.GetCurrentFramePtr();
            if (ptr == IntPtr.Zero)
                return; // No frame yet

            // Each pixel is 4 bytes in BGRA. 'data.Length' is total pixel count.
            Marshal.Copy(ptr, data, 0, data.Length);

            // Mark CPU data dirty so next toDevice(...) call re-uploads to GPU
            cpu_dirty = true;
        }

        public new void Dispose()
        {
            videoReader?.Dispose();
            base.Dispose();
        }
    }
}
