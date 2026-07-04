using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using RE.Core.Assets;
using RE.Rendering.Texturing;
using RE.Utils;

namespace RE.Rendering;

public class PostProcessLayer
{
    public static readonly PostProcessLayer Default = new("Assets/Shaders/Pass/Postprocess/default.frag");

    private const string DefaultVertexShader = "Assets/Shaders/Pass/Postprocess/default.vert";
    private static readonly int FullscreenVao;
    private readonly Framebuffer _buffer;

    public ShaderProgram Program { get; }
    public Shader FragmentShader => Program.Shaders[0];

    private Vector2 _oldSize;

    static PostProcessLayer()
    {
        GL.GenVertexArrays(1, out FullscreenVao);
    }

    public PostProcessLayer(string fragmentShader)
    {
        Program = new ShaderProgram();
        Program.AttachShader(fragmentShader);
        Program.AttachShader(DefaultVertexShader);

        _buffer = new Framebuffer("Postprocess " + Path.GetFileName(fragmentShader));
    }

    internal Framebuffer Draw(Framebuffer previous) => Draw(previous.ColorTexture);

    internal Framebuffer Draw(int texture)
    {
        if (_oldSize != Game.Instance.ClientSize)
        {
            _buffer.Resize(Game.Instance.ClientSize.X, Game.Instance.ClientSize.Y);
            _oldSize = Game.Instance.ClientSize;
        }

        if (this != Default)
            _buffer.Bind();
        else
            _buffer.Unbind();

        GL.ClearColor(Color4.Black);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        Program.Use();
        Program.SetValue("u_Texture", 0);

        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, texture);

        GL.BindVertexArray(FullscreenVao);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3);

        return _buffer;
    }
}