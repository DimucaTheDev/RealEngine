using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;
using RE.Rendering.Texturing;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace RE.Core.World.Testing
{
    public class ProcessWindowsTexture : StaticTexture
    {
        private readonly string _processName;
        private IntPtr _hwnd;
        private int _textureId;

        public ProcessWindowsTexture(string processName)
            : base(Array.Empty<byte>(), 0, 0, ImageColorType.Rgb)
        {
            _processName = processName;
            _textureId = GL.GenTexture();
            FindWindow();
        }

        private void FindWindow()
        {
            var processes = Process.GetProcessesByName(_processName);
            if (processes.Length > 0)
                _hwnd = processes[0].MainWindowHandle;
        }

        void RefreshTexture()
        {
            if (_hwnd == IntPtr.Zero)
            {
                FindWindow();
                if (_hwnd == IntPtr.Zero)
                    return;
            }

            GetClientRect(_hwnd, out RECT rect);
            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            if (width <= 0 || height <= 0)
                return;

            using var bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            using var g = Graphics.FromImage(bmp);

            IntPtr hdc = g.GetHdc();
            IntPtr src = GetDC(_hwnd);

            BitBlt(hdc, 0, 0, width, height, src, 0, 0, CopyPixelOperation.SourceCopy);

            ReleaseDC(_hwnd, src);
            g.ReleaseHdc(hdc);

            var data = bmp.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format24bppRgb);

            GL.BindTexture(TextureTarget.Texture2D, _textureId);

            if (Width != width || Height != height)
            {
               // Width = width;
               // Height = height;

                GL.TexImage2D(TextureTarget.Texture2D, 0,
                    PixelInternalFormat.Rgb,
                    width, height, 0, OpenTK.Graphics.OpenGL.PixelFormat.Bgr,
                    PixelType.UnsignedByte,
                    data.Scan0);

                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            }
            else
            {
                GL.TexSubImage2D(TextureTarget.Texture2D, 0,
                    0, 0, width, height, OpenTK.Graphics.OpenGL.PixelFormat.Bgr,
                    PixelType.UnsignedByte,
                    data.Scan0);
            }

            bmp.UnlockBits(data);
        }

        public override uint AsOpenGl()
        {
            RefreshTexture();
            return (uint)_textureId;
        }

        // WinAPI
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
        [DllImport("gdi32.dll")]
        static extern bool BitBlt(
            IntPtr hdcDest, int xDest, int yDest, int w, int h,
            IntPtr hdcSrc, int xSrc, int ySrc, CopyPixelOperation rop);

        struct RECT { public int Left, Top, Right, Bottom; }
    }
}
