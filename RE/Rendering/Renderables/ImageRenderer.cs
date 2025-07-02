using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core;
using StbImageSharp;

namespace RE.Rendering.Renderables;

public class ImageRenderer : Renderable
{
    private int _texture;
    private int _vao, _vbo, _ebo;
    private int _shaderProgram;
    private string _pathToImg;

    public override RenderLayer RenderLayer => RenderLayer.UI;
    public override bool IsVisible { get; set; } = true;
    public Vector2 Position { get; set; }
    public Vector2 Scale { get; set; }

    public ImageRenderer(string pathToImg, Vector2 pos, Vector2? size = null)
    {
        _pathToImg = pathToImg;
        Position = pos;
        Scale = size ?? new Vector2(100, 100);

        _shaderProgram = CompileShaders();
        _texture = LoadTexture(_pathToImg);
        SetupQuad();
    }

    public void ReplaceImage(string path) => _texture = LoadTexture(path);
    public override void Render(FrameEventArgs args)
    {
        GL.UseProgram(_shaderProgram);

        Matrix4 model = Matrix4.CreateScale(Scale.X, Scale.Y, 1f) * Matrix4.CreateTranslation(Position.X, Position.Y, 1);
        Matrix4 projection = Matrix4.CreateOrthographicOffCenter(0, Game.Instance.ClientSize.X, Game.Instance.ClientSize.Y, 0, -1, 1);

        GL.UniformMatrix4(GL.GetUniformLocation(_shaderProgram, "uModel"), false, ref model);
        GL.UniformMatrix4(GL.GetUniformLocation(_shaderProgram, "uProjection"), false, ref projection);

        GL.BindTexture(TextureTarget.Texture2D, _texture);
        GL.BindVertexArray(_vao);
        GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);
    }

    private void SetupQuad()
    {
        float[] vertices = {
            // pos       // uv
             0f,  0f,     0f, 0f,
             1f,  0f,     1f, 0f,
             1f,  1f,     1f, 1f,
             0f,  1f,     0f, 1f,
        };

        uint[] indices = { 0, 1, 2, 2, 3, 0 };

        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();
        _ebo = GL.GenBuffer();

        GL.BindVertexArray(_vao);

        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
        GL.EnableVertexAttribArray(0);

        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));
        GL.EnableVertexAttribArray(1);

        GL.BindVertexArray(0);
    }

    private int CompileShaders()
    {
        string vertexShaderSrc = File.ReadAllText("assets/shaders/ui_image.vert");

        string fragmentShaderSrc = File.ReadAllText("assets/shaders/ui_image.frag");

        int vertexShader = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vertexShader, vertexShaderSrc);
        GL.CompileShader(vertexShader);

        int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(fragmentShader, fragmentShaderSrc);
        GL.CompileShader(fragmentShader);

        int program = GL.CreateProgram();
        GL.AttachShader(program, vertexShader);
        GL.AttachShader(program, fragmentShader);
        GL.LinkProgram(program);
        GL.DetachShader(program, vertexShader);
        GL.DetachShader(program, fragmentShader);
        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);

        return program;
    }

    private int LoadTexture(string path)
    {
        int texID = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, texID);

        using var stream = File.OpenRead(path);
        var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
            image.Width, image.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, image.Data);

        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        return texID;
    }

    public void Dispose()
    {
        GL.DeleteTexture(_texture);
        GL.DeleteBuffer(_vbo);
        GL.DeleteBuffer(_ebo);
        GL.DeleteVertexArray(_vao);
        GL.DeleteProgram(_shaderProgram);
    }
}
