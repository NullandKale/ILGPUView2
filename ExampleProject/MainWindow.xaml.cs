using ExampleProject.Modes;
using GPU;
using Modes;
using System;
using System.Windows;
using System.Windows.Input;
using UIElement;

namespace ExampleProject
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IDisposable
    {
        public RenderManager renderManager;

        public MainWindow()
        {
            InitializeComponent();

            renderManager = new RenderManager();
            UIBuilder.SetFPSCallback((string fps) => { label.Content= fps; });
            UIBuilder.SetUIStack(rendermode_ui);

            AddRenderModes();
        }

        public void AddRenderModes(int default_mode = 1)
        {
            renderManager.AddRenderCallback(0, new Modes.Debug());
            renderManager.AddRenderCallback(1, new DrawCircles());
            renderManager.AddRenderCallback(2, new GOL());
            renderManager.AddRenderCallback(3, new Fractal());
            renderManager.AddRenderCallback(4, new Particles());
            renderManager.AddRenderCallback(5, new ImageFilter());
            renderManager.AddRenderCallback(6, new DebugRT());

            mode.ItemsSource = new string[] { 
                "Debug",
                "Draw Circles", 
                "Game of Life", 
                "Fractal", 
                "Particle Sim", 
                "Image Filter", 
                "DebugRT" };

            mode.SelectionChanged += (sender, args) =>
            {
                if (mode.SelectedIndex != -1)
                {
                    renderManager.SetRenderMode(mode.SelectedIndex);
                }
            };

            mode.SelectedIndex = default_mode;
        }

        public void Dispose()
        {
            renderManager.Dispose();
        }

        private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }

            if (e.Key == Key.Enter)
            {
                if (renderManager.renderWindow.WindowState == WindowState.Maximized)
                {
                    renderManager.renderWindow.SetWindowStyle(WindowStyle.ThreeDBorderWindow, WindowState.Normal);
                }
                else
                {
                    renderManager.renderWindow.SetWindowStyle(WindowStyle.None, WindowState.Maximized);
                }

                e.Handled = true;
            }

            if (e.Key >= Key.D0 && e.Key <= Key.D9)
            {
                string valString = new KeyConverter().ConvertToString(e.Key)!;
                int val = int.Parse(valString);
                renderManager.SetRenderModeMode(val);
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Dispose();
        }
    }
}
