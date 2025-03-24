using GPU;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace UIElement
{
    /// <summary>
    /// Interaction logic for RenderWindow.xaml
    /// </summary>
    public partial class RenderWindow : Window, IDisposable
    {
        Renderer gpu;
        IRenderCallback callback;
        bool loaded = false;

        public RenderWindow(IRenderCallback callback)
        {
            InitializeComponent();

            this.callback = callback;
            renderFrame.BeforeResolutionChanged = callback.BeforeResolutionChanged;

            gpu = new Renderer(renderFrame);

            Loaded += Window_Loaded;

            // Set up key event handler
            KeyDown += Window_KeyDown;
        }

        public bool TryStart()
        {
            if (callback != null && loaded)
            {
                callback.OnStart(gpu);
                UIBuilder.Clear();
                callback.CreateUI();

                gpu.Start(callback.OnRender, callback.OnLateRender);

                return true;
            }

            return false;
        }

        public void Stop()
        {
            if (callback != null)
            {
                callback.OnStop();
            }

            gpu.Dispose();
        }

        public void Dispose()
        {
            Stop();
        }

        public void SetPosition(int x, int y)
        {
            Left = x;
            Top = y;
        }

        public void SetWindowStyle(WindowStyle style, WindowState state)
        {
            WindowStyle = style;
            WindowState = state;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            loaded = true;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Call OnKeyPressed with the key pressed event and the current modifier keys
            callback?.OnKeyPressed(e.Key, Keyboard.Modifiers);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            this.Dispose();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
        }
    }

    public interface IRenderCallback
    {
        public void SetMode(int mode);
        public void CreateUI();
        public void OnStart(Renderer gpu);
        public void OnRender(Renderer gpu);
        public void OnKeyPressed(Key key, ModifierKeys modifiers);
        public void OnStop();
        public void OnLateRender(Renderer obj);
        public (int xSize, int ySize, bool update) BeforeResolutionChanged(RenderWindow renderWindow, int newWidth, int newHeight);
    }

}
