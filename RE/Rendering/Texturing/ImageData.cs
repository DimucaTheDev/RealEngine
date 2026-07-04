using System.Resources;
using JetBrains.Annotations;
using OpenTK.Graphics.OpenGL;
using RE.Core.Assets;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace RE.Rendering.Texturing;

public class ImageData()
{
    public byte[] PixelData { get; set; } = [];
    public int Width { get; set; }
    public int Height { get; set; }
    public PixelFormat Format { get; set; }

    public ImageData(byte[] pixels, int width, int height, PixelFormat format = PixelFormat.Rgba) : this()
    {
        PixelData = pixels;
        Width = width;
        Height = height;
        Format = format;
    }

    public static ImageData FromResource(string resourceName)
    {
        ImageData imageData = new();

        var data = ContentManager.GetBytes(resourceName);
        using var image = Image.Load<Rgba32>(data);

        imageData.PixelData = new byte[image.Width * image.Height * 4];
        imageData.Width = image.Width;
        imageData.Height = image.Height;
        imageData.Format = PixelFormat.Rgba;
        image.CopyPixelDataTo(imageData.PixelData);

        return imageData;
    }
}