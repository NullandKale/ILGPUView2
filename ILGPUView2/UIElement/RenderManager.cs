using GPU;
using System;
using System.Collections.Generic;
using System.Windows;

namespace UIElement
{
    public class RenderManager : IDisposable
    {
        public Dictionary<int, IRenderCallback> renderModes;
        public RenderWindow renderWindow;
        public int currentRenderMode;

        public RenderManager()
        {
            renderModes = new Dictionary<int, IRenderCallback>();

        }

        public void AddRenderCallback(int id, IRenderCallback renderCallback)
        {
            if (renderCallback != null)
            {
                renderModes[id] = renderCallback;
            }
        }

        public void SetRenderMode(int val)
        {
            if (renderModes.ContainsKey(val))
            {
                IRenderCallback newRenderMode = renderModes[val];
                currentRenderMode = val;
                if (renderWindow != null)
                {
                    renderWindow.Close();
                }

                //Application.Current.MainWindow.Hide();

                renderWindow = new RenderWindow(newRenderMode);

                renderWindow.Show();
                renderWindow.TryStart();
            }
        }

        public void Dispose()
        {
            if (renderWindow != null)
            {
                renderWindow.Close();
            }
        }

        public IRenderCallback? GetRenderCallback()
        {
            if (renderModes.ContainsKey(currentRenderMode))
            {
                IRenderCallback current = renderModes[currentRenderMode];
                return current;
            }

            return null;
        }

        public void SetRenderModeMode(int val)
        {
            GetRenderCallback()?.SetMode(val);
        }
    }
}
