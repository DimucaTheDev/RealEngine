using Hexa.NET.ImGui;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using RE.Utils;
using Serilog;
using Buffer = System.Buffer;

namespace RE.Rendering.Texturing
{
    public class StaticTexture : Texture
    {
        public static readonly Func<string, StaticTexture> DefaultCacheFactory = path => new(path);

        private ImTextureRef _imTexture;
        private uint _handle;
        private bool _deleted;

        public int Width { get; private set; }
        public int Height { get; private set; }
        public PixelInternalFormat InternalFormat { get; }


        public StaticTexture(int width, int height, PixelInternalFormat format = PixelInternalFormat.Rgba8)
        {
            Width = width;
            Height = height;
            InternalFormat = format;

            _handle = (uint)GL.GenTexture();
            _imTexture = new ImTextureRef { TexID = new ImTextureID(_handle) };

            Bind();

            AllocateStorage(width, height);
            SetDefaultParameters();

            GL.BindTexture(TextureTarget.Texture2D, 0);

            LabelObject();
        }

        public StaticTexture(ImageData data) : this(data.Width, data.Height)
        {
            SetData(data);
        }

        public StaticTexture(string resourceLocation) : this(1, 1)
        {
            ImageData image = ImageData.FromResource(resourceLocation);

            SetData(image);
        }

        protected StaticTexture()
        {
            _imTexture = default;
        }

        public void SetData(ImageData image)
        {
            if (image.Width != Width || image.Height != Height)
                Resize(image.Width, image.Height);

            Bind();

            GL.TexSubImage2D(
                TextureTarget.Texture2D,
                0,
                0,
                0,
                image.Width,
                image.Height,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                image.PixelData);

            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        public void Resize(int width, int height)
        {
            Width = width;
            Height = height;

            Bind();

            AllocateStorage(width, height);

            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        public void Bind(TextureUnit unit = TextureUnit.Texture0)
        {
            ThrowIfDeleted();

            GL.ActiveTexture(unit);
            GL.BindTexture(TextureTarget.Texture2D, _handle);
        }

        public override uint AsOpenGl()
        {
            ThrowIfDeleted();
            return _handle;
        }

        public override ImTextureRef AsImGui()
        {
            ThrowIfDeleted();
            return _imTexture;
        }

        public override void Delete()
        {
            if (_deleted)
                return;

            GL.DeleteTexture(_handle);

            Log.Verbose("Deleted texture {Id}", _handle);

            _handle = 0;
            _deleted = true;

            base.OnUnload();
        }

        private void ThrowIfDeleted()
        {
            if (_deleted)
                throw new InvalidOperationException("Texture has been deleted.");
        }

        private void AllocateStorage(int width, int height)
        {
            GL.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat, width, height, 0, PixelFormat.Rgba,
                PixelType.UnsignedByte, IntPtr.Zero);
        }

        private static void SetDefaultParameters()
        {
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                (int)TextureMinFilter.Nearest);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                (int)TextureMagFilter.Nearest);
        }

        private void LabelObject()
        {
            if (!AssetPath.IsNullOrEmpty())
            {
                GL.ObjectLabel(
                    ObjectLabelIdentifier.Texture,
                    _handle,
                    -1,
                    AssetPath);
            }
        }

        public static StaticTexture CreateMonoColorTexture(Vector4 color)
        {
            byte[] pixels =
            [
                (byte)(color.X * 255),
                (byte)(color.Y * 255),
                (byte)(color.Z * 255),
                (byte)(color.W * 255)
            ];

            var tex = new StaticTexture(1, 1);
            tex.SetData(new ImageData(pixels, 1, 1));

            return tex;
        }

        public static StaticTexture CreateMonoColorTexture(Vector3 color)
            => CreateMonoColorTexture(new Vector4(color, 1));

        public static StaticTexture CreateMissingTexture(
            int size = 64,
            byte[]? color1 = null,
            byte[]? color2 = null)
        {
            byte[] pixels = new byte[size * size * 4];

            byte[] a = color1 ?? [110, 110, 110, 255];
            byte[] b = color2 ?? [35, 35, 35, 255];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Buffer.BlockCopy(
                        ((x + y) & 1) == 0 ? a : b,
                        0,
                        pixels,
                        (y * size + x) * 4,
                        4);
                }
            }

            var tex = new StaticTexture(size, size);
            tex.SetData(new ImageData(pixels, size, size));

            return tex;
        }

        public static StaticTexture FromGlHandle(uint handle, int width, int height,
            PixelInternalFormat format = PixelInternalFormat.Rgba8)
        {
            return new StaticTexture
            {
                _handle = handle,
                _imTexture = new ImTextureRef { TexID = handle },
                Width = width,
                Height = height,
            };
        }

        public static implicit operator ImTextureRef(StaticTexture texture) => texture.AsImGui();
    }
}