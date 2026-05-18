using System.Runtime.InteropServices;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace RE.Utils
{
    internal class WinApi
    {
        private const uint FLASHW_STOP = 0;
        private const uint FLASHW_CAPTION = 1;
        private const uint FLASHW_TRAY = 2;
        private const uint FLASHW_ALL = 3;
        private const uint FLASHW_TIMER = 4;
        private const uint FLASHW_TIMERNOFG = 12;
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_APPWINDOW = 0x00040000;
        private const int GWLP_HWNDPARENT = -8;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int x, int y);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern void FreeLibrary(nint handle);

        [DllImport("user32.dll", EntryPoint = "MessageBoxW", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int MessageBox(IntPtr hWnd, string lpText, string lpCaption, uint uType);

        [DllImport("psapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetProcessMemoryInfo(IntPtr Process, out PROCESS_MEMORY_COUNTERS ppsmemCounters, uint cb);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        [DllImport("user32.dll", SetLastError = true)]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        public static unsafe void StartFlashing(IntPtr hWnd, uint count = 5)
        {
            FLASHWINFO fInfo = new FLASHWINFO();

            fInfo.cbSize = Convert.ToUInt32(Marshal.SizeOf(fInfo));
            fInfo.hwnd = GLFW.GetWin32Window((Window*)hWnd);
            fInfo.dwFlags = FLASHW_TRAY | FLASHW_TIMERNOFG;
            fInfo.uCount = count;
            fInfo.dwTimeout = 0;

            FlashWindowEx(ref fInfo);
        }

        public static void StopFlashing(IntPtr hWnd)
        {
            FLASHWINFO fInfo = new FLASHWINFO();

            fInfo.cbSize = Convert.ToUInt32(Marshal.SizeOf(fInfo));
            fInfo.hwnd = hWnd;
            fInfo.dwFlags = FLASHW_STOP;
            fInfo.uCount = 0;
            fInfo.dwTimeout = 0;

            FlashWindowEx(ref fInfo);
        }

        public static void HideFromTaskbar(IntPtr hwnd, IntPtr owner)
        {
            return;
            SetWindowLongPtr(hwnd, GWLP_HWNDPARENT, owner);

            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_TOOLWINDOW;
            exStyle &= ~WS_EX_APPWINDOW;
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);

            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct FLASHWINFO
    {
        public uint cbSize;
        public IntPtr hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_MEMORY_COUNTERS
    {
        public uint cb;
        public uint PageFaultCount;
        public long PeakWorkingSetSize;
        public long WorkingSetSize;
        public long QuotaPeakPagedPoolUsage;
        public long QuotaPagedPoolUsage;
        public long QuotaPeakNonPagedPoolUsage;
        public long QuotaNonPagedPoolUsage;
        public long PagefileUsage;
        public long PeakPagefileUsage;
    }
}
