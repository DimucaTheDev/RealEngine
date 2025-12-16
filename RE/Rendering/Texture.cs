using System.Runtime.CompilerServices;
using Hexa.NET.ImGui;
using JetBrains.Annotations;
using OpenTK.Graphics.OpenGL4;
using RE.Core.Assets;
using Serilog;
using StbImageSharp;

namespace RE.Rendering
{
    public class Texture : DynamicAsset
    {
        private uint _glHandle;
        private ImTextureRef? _imHandle;
        private bool _deleted;
        private readonly bool _keepTextureData;
        private byte[]? _imageData;

        public byte[]? ImageData
        {
            get
            {
                if (!_keepTextureData)
                    throw new InvalidOperationException("Texture data is not stored.");
                return _imageData;
            }
            private set => _imageData = value;
        }
        public int Width { get; }
        public int Height { get; }

        public Texture(string resourceLocation, bool keepTextureData = false)
        {
            using var stream = ContentManager.Open(resourceLocation);
            var image = ImageResult.FromStream(stream);
            ImageData = image.Data;
            Width = image.Width;
            Height = image.Height;
            _keepTextureData = keepTextureData;
        }
        public Texture(byte[] data, int width, int height, bool keepTextureData = false)
        {
            ImageData = data;
            Width = width;
            Height = height;
            _keepTextureData = keepTextureData;
        }

        public uint AsOpenGl()
        {
            ThrowIfDeleted();
            if (_glHandle == 0)
            {
                uint texId = (uint)GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, texId);

                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                    Width, Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, _imageData);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

                GL.BindTexture(TextureTarget.Texture2D, 0);

                if (!_keepTextureData)
                    ImageData = null;

                if (GL.GetError() != ErrorCode.NoError)
                {
                    Delete();
                    throw new GlException("Unable to create texture.");
                }

                Log.Verbose("New texture {Id}", texId);

                _glHandle = texId;
            }

            return _glHandle;
        }

        public ImTextureRef AsImGui()
        {
            return _imHandle ??= new ImTextureRef() { TexID = new ImTextureID(AsOpenGl()) };
        }

        public void Delete([CallerMemberName] string method = "")
        {
            if (_glHandle == 0)
            {
                Log.Warning("Redundant texture deletion from {name}", method);
                return;
            }
            GL.DeleteTexture(_glHandle);
            Log.Verbose("Delete texture {Id}", _glHandle);
            _glHandle = 0;
            _imHandle = null;
            _deleted = true;
            base.OnUnload();
        }

        private void ThrowIfDeleted()
        {
            if (_deleted)
                throw new InvalidOperationException("Tried to use a deleted texture.");
        }

        //public static implicit operator uint(Texture texture) => texture.AsOpenGl();
        public static implicit operator ImTextureRef(Texture texture) => texture.AsImGui();
    }
}