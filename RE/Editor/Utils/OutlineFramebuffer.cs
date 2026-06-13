using OpenTK.Graphics.OpenGL;
using RE.Rendering;
using RE.Utils;

namespace RE.Editor.Utils;

internal class OutlineFramebuffer : IDisposable
{
    public int Handle { get; private set; }
    public int DepthStencilTexture { get; private set; }

    public int Width { get; private set; }
    public int Height { get; private set; }

    public OutlineFramebuffer() : this(Game.Instance.ClientSize.X, Game.Instance.ClientSize.Y)
    {
    }

    public OutlineFramebuffer(int width, int height)
    {
        Create(width, height);
    }

    private void Create(int width, int height)
    {
        Width = width;
        Height = height;

        Handle = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, Handle);

        DepthStencilTexture = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, DepthStencilTexture);

        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Depth24Stencil8,
            width, height, 0, PixelFormat.DepthStencil, PixelType.UnsignedInt248, IntPtr.Zero);

        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.DepthStencilTextureMode, (int)All.StencilIndex);

        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment,
            TextureTarget.Texture2D, DepthStencilTexture, 0);

        GL.DrawBuffer(DrawBufferMode.None);
        GL.ReadBuffer(ReadBufferMode.None);

        var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != FramebufferErrorCode.FramebufferComplete)
        {
            throw new GlException($"Outline Framebuffer is not complete: {status}");
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
        if (DepthStencilTexture != 0) GL.DeleteTexture(DepthStencilTexture);

        Handle = 0;
        DepthStencilTexture = 0;

        GC.SuppressFinalize(this);
    }
}