using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;
using PixelFormat = OpenTK.Graphics.OpenGL.PixelFormat;

namespace RE.Utils;

internal static class WindowsShell
{
    public static readonly Dictionary<string, IntPtr> _iconCache = new Dictionary<string, IntPtr>();
    public static IntPtr _folderIconId = IntPtr.Zero;

    public static IntPtr GetSystemIcon(string extensionOrPath, SHGFI flags = SHGFI.LargeIcon | SHGFI.Icon)
    {

        if (_iconCache.TryGetValue(extensionOrPath, out IntPtr cachedId))
        {
            return cachedId;
        }

        SHFILEINFO shfi = new SHFILEINFO();
        uint dwAttribs = 0;

        if (!Directory.Exists(extensionOrPath))
        {
            dwAttribs = (uint)FileAttributes.Normal;
            flags |= SHGFI.UseFileAttributes;
        }
        else
        {
            dwAttribs = (uint)FileAttributes.Directory;
            flags |= SHGFI.UseFileAttributes;
        }

        IntPtr handle = SHGetFileInfo(extensionOrPath, dwAttribs, ref shfi, (uint)Marshal.SizeOf(shfi), (uint)flags);

        if (handle == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        IntPtr textureId = IntPtr.Zero;

        try
        {
            using Icon icon = Icon.FromHandle(shfi.hIcon);
            using Bitmap bitmap = icon.ToBitmap();
            textureId = ConvertBitmapToImGuiTexture(bitmap);
        }
        finally
        {
            DestroyIcon(shfi.hIcon);
        }

        if (textureId != IntPtr.Zero)
        {
            _iconCache[extensionOrPath] = textureId;
        }

        return textureId;
    }

    private static IntPtr ConvertBitmapToImGuiTexture(Bitmap bitmap)
    {
        int textureId = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, textureId);
        var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        GL.TexImage2D(TextureTarget.Texture2D,
            level: 0,
            internalformat: PixelInternalFormat.Rgba,
            width: data.Width,
            height: data.Height,
            border: 0,
            format: PixelFormat.Bgra,
            type: PixelType.UnsignedByte,
            pixels: data.Scan0);

        bitmap.UnlockBits(data);

        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

        GL.BindTexture(TextureTarget.Texture2D, 0);
        return textureId;
    }

    public static IntPtr GetFileIcon(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension))
        {
            extension = "file_no_ext";
        }

        if (_iconCache.TryGetValue(extension, out IntPtr cachedId))
        {
            return cachedId;
        }

        return GetSystemIcon(filePath);
    }


    [Flags]
    public enum SHGFI : uint
    {
        Icon = 0x000000100,
        UseFileAttributes = 0x000000010,
        OpenIcon = 0x000000002,
        SmallIcon = 0x000000001,
        LargeIcon = 0x000000000,
        SysIconIndex = 0x000004000
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SHFILEINFO
    {
        public const int NAMESIZE = 80;
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = NAMESIZE)]
        public string szTypeName;
    }

    [DllImport("shell32.dll")]
    public static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref SHFILEINFO psfi,
        uint cbSizeFileInfo,
        uint uFlags
    );

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyIcon(IntPtr hIcon);
}