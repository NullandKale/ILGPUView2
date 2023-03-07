using GPU;
using System;
using System.Windows;
using System.Windows.Input;

namespace UIElement
{
    /// <summary>
    /// Interaction logic for RenderWindow.xaml
    /// </summary>
    public partial class RenderWindow : Window, IDisposable
    {
        Device gpu;
        IRenderCallback callback;
        bool loaded = false;

        public RenderWindow()
        {
            InitializeComponent();

            gpu = new Device(renderFrame);

            Loaded += Window_Loaded;

            // Set up key event handler
            KeyDown += Window_KeyDown;
        }

        public bool TryStart(IRenderCallback callback)
        {
            this.callback = callback;
            if (callback != null && loaded)
            {
                callback.OnStart(gpu);
                callback.CreateUI();

                gpu.Start(callback.OnRender);

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
    }

    public interface IRenderCallback
    {
        public void SetMode(int mode);
        public void CreateUI();
        public void OnStart(Device gpu);
        public void OnRender(Device gpu);
        public void OnKeyPressed(Key key, ModifierKeys modifiers);
        public void OnStop();
    }

}
