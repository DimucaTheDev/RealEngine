using System.Runtime.InteropServices;

namespace RE.Utils
{
    internal class WinApi
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern void FreeLibrary(nint handle);

        [DllImport("user32.dll", EntryPoint = "MessageBoxW",
            CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
        public static extern int MessageBox(IntPtr hWnd, string lpText, string lpCaption, uint uType);

    }
}
