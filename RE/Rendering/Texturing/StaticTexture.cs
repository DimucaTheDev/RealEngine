using Hexa.NET.ImGui;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using RE.Core.Assets;
using RE.Utils;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using StbImageSharp;
using Buffer = System.Buffer;
using Image = SixLabors.ImageSharp.Image;
using Size = SixLabors.ImageSharp.Size;

namespace RE.Rendering.Texturing
{
    /// <summary>
    /// Represents a managed texture object that can be used in ImGui or OpenGL.
    /// </summary>
    public class StaticTexture : Texture
    {
        public enum ImageColorType
        {
            Rgb,
            Rgba
        }

        public static readonly Func<string, StaticTexture> DefaultCacheFactory = s => new StaticTexture(s);

        private uint _glHandle;
        private ImTextureRef? _imHandle;
        private bool _deleted;
        private readonly bool _keepTextureData;
        private byte[]? _imageData;

        /// <summary>
        /// Raw image data in RGBA format.
        /// </summary>
        /// <remarks>
        /// This property will throw exception if <c>keepTextureData</c> was set to <see langword="false"/> in constructor.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Image data is not stored after initialisation</exception>
        public byte[]? ImageData
        {
            get
            {
                if (!_keepTextureData)
                    throw new InvalidOperationException("StaticTexture data is not stored.");
                return _imageData;
            }
            private set => _imageData = value;
        }

        /// <summary>
        /// Get width of an image in pixels
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Get height of an image in pixels
        /// </summary>
        public int Height { get; }

        public ImageColorType ColorType { get; set; }

        private StaticTexture()
        {
        }

        /// <summary>
        /// Create new StaticTexture object and load image from <paramref name="resourceLocation"/>.
        /// </summary>
        /// <param name="resourceLocation">Location of a file to load image from.</param>
        /// <param name="keepTextureData">Whether keep image data in <see cref="ImageData"/> or not.</param>
        public StaticTexture(string resourceLocation, ImageColorType type = ImageColorType.Rgba,
            bool keepTextureData = false)
        {
            using var stream = ContentManager.Open(resourceLocation);
            var image = ImageResult.FromStream(stream,
                type == ImageColorType.Rgba ? ColorComponents.RedGreenBlueAlpha : ColorComponents.RedGreenBlue);

            ImageData = image.Data;

            if (Fun.PixelateAllTextures) // #FunSettings: pixelate textures for retro look
            {
                int p = 128;
                var load = Image.Load(ContentManager.GetBytes(resourceLocation));
                load.Mutate(s => s.Resize(new Size(p, p), new NearestNeighborResampler(), true));
                load.Mutate(s => s.Resize(new Size(image.Width, image.Height), new NearestNeighborResampler(), true));
                Image<Rgba32> converted = load.CloneAs<Rgba32>();
                Span<byte> d = new Span<byte>(new byte[converted.Width * converted.Height * 4]);
                converted.CopyPixelDataTo(d);
                ImageData = d.ToArray();
            }

            Width = image.Width;
            Height = image.Height;
            ColorType = type;
            _keepTextureData = keepTextureData;
        }

        /// <summary>
        /// Create new StaticTexture object and set <see cref="ImageData"/> to <paramref name="data"/>.
        /// </summary>
        /// <param name="data">Raw image data in RGBA format.</param>
        /// <param name="width"></param>
        /// <param name="height">Height of an image</param>
        /// <param name="keepTextureData">Whether keep image data in <see cref="ImageData"/> or not.</param>
        /// <exception cref="InvalidOperationException">Image data is not in RGBA format.</exception>
        public StaticTexture(byte[] data, int width, int height, ImageColorType type = ImageColorType.Rgba,
            bool keepTextureData = false)
        {
            if (type == ImageColorType.Rgba && data.Length % 4 != 0)
                throw new InvalidOperationException("Specified image data is not in RGBA format.");
            if (type == ImageColorType.Rgb && data.Length % 3 != 0)
                throw new InvalidOperationException("Specified image data is not in RGB format.");

            ImageData = data;

            if (Fun.PixelateAllTextures) // #FunSettings: pixelate textures for retro look
            {
                int p = 128;
                var converted = Image.LoadPixelData<Rgba32>(data, width, height);
                converted.Mutate(s => s.Resize(new Size(p, p), new NearestNeighborResampler(), true));
                converted.Mutate(s => s.Resize(new Size(width, height), new NearestNeighborResampler(), true));
                var d = new Span<byte>(new byte[converted.Width * converted.Height * 4]);
                converted.CopyPixelDataTo(d);
                ImageData = d.ToArray();
            }

            Width = width;
            Height = height;
            ColorType = type;
            _keepTextureData = keepTextureData;
            CreateGlTexture();
        }

        /// <summary>
        /// Get OpenGL handle for this instance.
        /// </summary> 
        /// <exception cref="GlException">StaticTexture is deleted and no more valid.</exception>
        public override uint AsOpenGl()
        {
            ThrowIfDeleted();
            if (_glHandle == 0)
                CreateGlTexture();

            return _glHandle;
        }

        /// <summary>
        /// Get ImGui texture reference of this instance.
        /// </summary> 
        public override ImTextureRef AsImGui()
        {
            return _imHandle ??= new ImTextureRef { TexID = new ImTextureID(AsOpenGl()) };
        }

        public override void Delete()
        {
            if (_glHandle == 0)
            {
                Log.Error("Redundant texture {Id} deletion.", _glHandle);
                return;
            }

            GL.DeleteTexture(_glHandle);
            Log.Verbose("Delete texture {Id}", _glHandle);
            _glHandle = 0;
            _imHandle = null;
            _deleted = true;
            base.OnUnload();
        }

        public static StaticTexture CreateMissingTexture(int size = 100, byte[]? color1 = null, byte[]? color2 = null)
        {
            byte[] data = new byte[size * size * 4];

            byte[] purple = color1 ?? [110, 110, 110, 255]; //[255, 0, 255, 255];
            byte[] black = color2 ?? [35, 35, 35, 255]; //[0, 0, 0, 255];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isPurple = (x + y) % 2 == 0;
                    byte[] color = isPurple ? purple : black;

                    int index = (y * size + x) * 4;
                    Buffer.BlockCopy(color, 0, data, index, 4);
                }
            }

            var t = new StaticTexture(data, size, size);
            return t;
        }

        public static StaticTexture CreateMonoColorTexture(Vector3 color) =>
            CreateMonoColorTexture(new Vector4(color, 1));

        public static StaticTexture CreateMonoColorTexture(Vector4 color)
        {
            byte[] data =
            [
                (byte)(color.X * 255f),
                (byte)(color.Y * 255f),
                (byte)(color.Z * 255f),
                (byte)(color.W * 255f)
            ];
            var t = new StaticTexture(data, 1, 1);
            return t;
        }

        private void ThrowIfDeleted()
        {
            if (_deleted)
                throw new InvalidOperationException("Tried to use a deleted texture.");
        }

        private uint CreateGlTexture()
        {
            uint texId = (uint)GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, texId);

            GL.TexImage2D(TextureTarget.Texture2D, 0,
                ColorType == ImageColorType.Rgba ? PixelInternalFormat.Rgba : PixelInternalFormat.Rgb,
                Width, Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, _imageData);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                (int)TextureMagFilter.Nearest);

            if (!AssetPath.IsNullOrEmpty())
                GL.ObjectLabel(ObjectLabelIdentifier.Texture, texId, -1, AssetPath);

            /*
            GL.TexParameter(TextureTarget.Texture2D,
                TextureParameterName.TextureWrapS,
                (int)TextureWrapMode.Repeat);

            GL.TexParameter(TextureTarget.Texture2D,
                TextureParameterName.TextureWrapT,
                (int)TextureWrapMode.Repeat);
            */
            GL.BindTexture(TextureTarget.Texture2D, 0);

            if (!_keepTextureData)
                ImageData = null;

            var error = GL.GetError();
            if (error != ErrorCode.NoError)
            {
                Delete();
                throw new GlException("Unable to create texture.");
            }

            Log.Verbose("New texture {Id}", texId);

            return _glHandle = texId;
        }

        //public static implicit operator uint(StaticTexture texture) => texture.AsOpenGl();
        public static implicit operator ImTextureRef(StaticTexture staticTexture) => staticTexture.AsImGui();

        public static StaticTexture FromGlHandle(uint handle)
        {
            return new StaticTexture
            {
                _glHandle = handle
                //todo
            };
        }
    }
}