using System.Runtime.InteropServices;

namespace RE.Utils
{
    internal class WinApi
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern void FreeLibrary(nint handle);
    }
}
