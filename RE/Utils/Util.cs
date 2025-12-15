using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace RE.Utils
{
    internal static class Util
    {
        public static uint CreateMissingTexture(int size = 100, byte[]? color1 = null, byte[]? color2 = null)
        {
            uint texID = (uint)GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, texID);

            byte[] data = new byte[size * size * 4];

            //byte[] r =
            //[
            //    (byte)Random.Shared.Next(255),
            //    (byte)Random.Shared.Next(255),
            //    (byte)Random.Shared.Next(255), 
            //    255
            //]; 
            // Random color for the texture


            byte[] purple = color1 ?? [255, 0, 255, 255];
            byte[] black = color2 ?? [0, 0, 0, 255];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isPurple = (x + y) % 2 == 0;
                    byte[] color = isPurple ? purple : black;

                    int index = (y * size + x) * 4;
                    System.Buffer.BlockCopy(color, 0, data, index, 4);
                }
            }

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                size, size, 0, PixelFormat.Rgba, PixelType.UnsignedByte, data);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

            return texID;
        }

        public static uint CreateMonoColorTexture(Vector3 color)
        {
            uint texID = (uint)GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, texID);

            byte[] data =
            [
                (byte)(color.X * 255f),
                (byte)(color.Y * 255f),
                (byte)(color.Z * 255f),
                255
            ];

            GL.TexImage2D(TextureTarget.Texture2D,
                0,
                PixelInternalFormat.Rgba, 1, 1, 0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                data);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

            GL.BindTexture(TextureTarget.Texture2D, 0);

            return texID;
        }
    }
}
