using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace GPU
{
    public class ScreenEnumerator
    {
        private List<ScreenInfo> screens = new List<ScreenInfo>();

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lpRECT, MonitorEnumProc callback, IntPtr dwData);

        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        public class ScreenInfo
        {
            public RECT MonitorArea;
            public RECT WorkArea;
            public bool IsPrimary;
        }

        public void EnumerateScreens()
        {
            screens.Clear();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                new MonitorEnumProc((IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
                {
                    MONITORINFO mi = new MONITORINFO();
                    mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));

                    if (GetMonitorInfo(hMonitor, ref mi))
                    {
                        screens.Add(new ScreenInfo
                        {
                            MonitorArea = mi.rcMonitor,
                            WorkArea = mi.rcWork,
                            IsPrimary = (mi.dwFlags & 1) != 0
                        });
                    }
                    else
                    {
                        Console.WriteLine("GetMonitorInfo failed");
                    }
                    return true;
                }), IntPtr.Zero);

            Console.WriteLine($"screens count: {screens.Count}");
        }

        public List<ScreenInfo> GetScreens()
        {
            EnumerateScreens();
            return screens;
        }

        public string[] GetScreenDescriptions()
        {
            var screenDescriptions = new List<string>();
            foreach (var screen in GetScreens())
            {
                screenDescriptions.Add($"Screen: {screen.MonitorArea.right - screen.MonitorArea.left}x{screen.MonitorArea.bottom - screen.MonitorArea.top} {screen.MonitorArea.left}, {screen.MonitorArea.top}");
            }
            return screenDescriptions.ToArray();
        }
    }
}
