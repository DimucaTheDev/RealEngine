using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using RE.Rendering;

namespace RE.Utils
{
    internal static class Util
    {
        public static Texture CreateMissingTexture(int size = 100, byte[]? color1 = null, byte[]? color2 = null)
        {
            byte[] data = new byte[size * size * 4];

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

            var t = new Texture(data, size, size);
            return t;
        }

        public static Texture CreateMonoColorTexture(Vector3 color) =>
            CreateMonoColorTexture(new Vector4(color.Xzy, 1));
        public static Texture CreateMonoColorTexture(Vector4 color)
        {
            byte[] data =
            [
                (byte)(color.X * 255f),
                (byte)(color.Y * 255f),
                (byte)(color.Z * 255f),
                (byte)(color.W * 255f)
            ];
            var t = new Texture(data, 1, 1);
            return t;
        }
    }
}
