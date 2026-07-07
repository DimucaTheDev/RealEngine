using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using RE.Utils;
using Serilog;

namespace RE.Rendering
{
    public class Framebuffer : IDisposable
    {
        public int Handle { get; private set; }
        public int ColorTexture { get; private set; }
        public int DepthStencilBuffer { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public bool SizeMatchesClientSize { get; set; } = false;

        private Vector2 _oldSize;

        public Framebuffer(string? objectName = null) : this(Game.Instance.ClientSize.X, Game.Instance.ClientSize.Y,
            objectName)
        {
        }

        public Framebuffer(int width, int height, string? objectName = null)
        {
            Create(width, height, objectName);
        }

        public static void BindDefault() => GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

        private void Create(int width, int height, string? objectName = null)
        {
            Width = width;
            Height = height;

            Handle = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, Handle);


            ColorTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, ColorTexture);

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS,
                (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT,
                (int)TextureWrapMode.ClampToEdge);

            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                TextureTarget.Texture2D, ColorTexture, 0);

            DepthStencilBuffer = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, DepthStencilBuffer);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.Depth24Stencil8, width, height);

            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment,
                RenderbufferTarget.Renderbuffer, DepthStencilBuffer);

            var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != FramebufferErrorCode.FramebufferComplete)
            {
                throw new GlException($"Framebuffer is not complete: {status}");
            }

            if (!objectName.IsNullOrEmpty())
            {
                GL.ObjectLabel(ObjectLabelIdentifier.Framebuffer, Handle, -1, objectName);

                string ctLabel = $"{objectName} - {nameof(ColorTexture)}";
                GL.ObjectLabel(ObjectLabelIdentifier.Texture, ColorTexture, -1, ctLabel);

                string dsbLabel = $"{objectName} - {nameof(DepthStencilBuffer)}";
                GL.ObjectLabel(ObjectLabelIdentifier.Renderbuffer, DepthStencilBuffer, -1, dsbLabel);
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        public void Bind()
        {
            if (SizeMatchesClientSize && _oldSize != Game.Instance.ClientSize)
            {
                Resize(Game.Instance.ClientSize.X, Game.Instance.ClientSize.Y);
                _oldSize = Game.Instance.ClientSize;
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, Handle);
            GL.Viewport(0, 0, Width, Height);
        }

        public void Unbind()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        public void Resize(int newWidth, int newHeight)
        {
            if (newWidth <= 0 || newHeight <= 0) return;
            if (newWidth == Width && newHeight == Height) return;

            Dispose();
            Create(newWidth, newHeight);
        }

        public void Dispose()
        {
            if (Handle != 0) GL.DeleteFramebuffer(Handle);
            if (ColorTexture != 0) GL.DeleteTexture(ColorTexture);
            if (DepthStencilBuffer != 0) GL.DeleteRenderbuffer(DepthStencilBuffer);

            Handle = 0;
            ColorTexture = 0;
            DepthStencilBuffer = 0;

            GC.SuppressFinalize(this);
        }
    }
}