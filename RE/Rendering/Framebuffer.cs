using OpenTK.Graphics.OpenGL4;
using RE.Utils;

namespace RE.Rendering
{
    public class Framebuffer : IDisposable
    {
        public int Handle { get; private set; }
        public int ColorTexture { get; private set; }
        public int DepthStencilBuffer { get; private set; }

        public int Width { get; private set; }
        public int Height { get; private set; }

        public Framebuffer() : this(Game.Instance.ClientSize.X, Game.Instance.ClientSize.Y)
        {
        }

        public Framebuffer(int width, int height)
        {
            Create(width, height);
        }

        private void Create(int width, int height)
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

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        public void Bind()
        {
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