using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using RE.Core.Assets;
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

    internal void Resize(int width, int height)
    {
        _buffer.Resize(width, height);
    }

    internal Framebuffer Draw(Framebuffer previous)
    {
        if (this != Default)
            _buffer.Bind();
        else
            _buffer.Unbind();

        GL.ClearColor(Color4.Black);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        Program.Use();
        Program.SetValue("u_Texture", 0);

        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, previous.ColorTexture);

        GL.BindVertexArray(FullscreenVao);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3);

        return _buffer;
    }
}