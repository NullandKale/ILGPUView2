using GPU;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace UIElement
{
    public partial class RenderFrame : UserControl
    {
        public int width;
        public int height;
        public WriteableBitmap wBitmap;
        public Int32Rect rect;

        public Func<RenderWindow, int, int, (int xSize, int ySize, bool update)> BeforeResolutionChanged;
        public Action<int, int> onResolutionChanged;

        public RenderFrame()
        {
            InitializeComponent();

            SizeChanged += RenderFrame_SizeChanged;
        }

        private void RenderFrame_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            width = (int)e.NewSize.Width;
            height = (int)e.NewSize.Height;

            if (BeforeResolutionChanged != null)
            {
                var parentWindow = Window.GetWindow(this);
                if (parentWindow != null)
                {
                    var size = BeforeResolutionChanged.Invoke(parentWindow as RenderWindow, width, height);
                    if (size.update)
                    {
                        width = size.xSize;
                        height = size.ySize;
                    }
                }
            }

            UpdateResolution();
        }

        private void UpdateResolution()
        {
            wBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            frame.Source = wBitmap;
            rect = new Int32Rect(0, 0, width, height);
            onResolutionChanged?.Invoke(width, height);
        }

        public void update(ref int[] data)
        {
            wBitmap.Lock();
            IntPtr pBackBuffer = wBitmap.BackBuffer;
            Marshal.Copy(data, 0, pBackBuffer, data.Length);
            wBitmap.AddDirtyRect(rect);
            wBitmap.Unlock();
        }
    }
}
