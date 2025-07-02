using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Utils;

namespace RE.Rendering.Text;

public class BillboardText3D : Renderable
{
    private readonly Dictionary<uint, Character> _characters;
    private readonly int _vao;
    private readonly int _vbo;
    private readonly int _shaderProgram;
    private readonly int _whiteTexture;
    private readonly bool _bottomToTop;
    private Color4 _bgColor, _textColor;
    private float _scale => Scale * 0.005f;

    public Vector3 Position { get; set; }
    public float Scale { get; set; }
    public string Text { get; set; }

    public static BillboardText3D I;

    public BillboardText3D(string content, Vector3 pos, FreeTypeFont font, bool bottomToTop = false)
        : this(content, pos, font, 1, Color4.White, new(0.3f, 0.3f, 0.3f, .5f), bottomToTop) { }
    public BillboardText3D(string content, Vector3 pos, FreeTypeFont font, float scale, Color4 textColor, Color4 bgColor, bool bottomToTop)
    {
        I = this;
        Position = pos;
        Text = content;
        Scale = scale;

        _bottomToTop = bottomToTop;
        _textColor = textColor;
        _bgColor = bgColor;
        _characters = font.CharacterMap.ToDictionary();
        _shaderProgram = LoadShaderProgram();


        float[] vertices = {
            //  x,     y,   u, v
            0.0f, -1.0f, 0.0f, 1.0f,
            0.0f,  0.0f, 0.0f, 0.0f,
            1.0f,  0.0f, 1.0f, 0.0f,

            0.0f, -1.0f, 0.0f, 1.0f,
            1.0f,  0.0f, 1.0f, 0.0f,
            1.0f, -1.0f, 1.0f, 1.0f
        };

        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();
        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));

        GL.BindVertexArray(0);

        _whiteTexture = CreateWhiteTexture();

        GL.Enable(EnableCap.DepthTest);
        GL.DepthFunc(DepthFunction.Less);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
    }

    private static int CreateWhiteTexture()
    {
        int tex = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, tex);
        byte[] px = { 255, 255, 255, 255 };
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, 1, 1, 0,
            PixelFormat.Rgba, PixelType.UnsignedByte, px);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        GL.BindTexture(TextureTarget.Texture2D, 0);
        return tex;
    }
    private static int LoadShaderProgram()
    {
        string vertexSource = File.ReadAllText("Assets/shaders/text_3d.vert");
        string fragmentSource = File.ReadAllText("Assets/shaders/text_3d.frag");

        int vertexShader = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vertexShader, vertexSource);
        GL.CompileShader(vertexShader);

        int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(fragmentShader, fragmentSource);
        GL.CompileShader(fragmentShader);

        int program = GL.CreateProgram();
        GL.AttachShader(program, vertexShader);
        GL.AttachShader(program, fragmentShader);
        GL.LinkProgram(program);

        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);

        return program;
    }

    public override void Render(FrameEventArgs args)
    {
        if (string.IsNullOrEmpty(Text))
            return;

        GL.UseProgram(_shaderProgram);

        var camPos = Camera.Instance.Position;
        var view = Camera.Instance.GetViewMatrix();
        var projection = Camera.Instance.GetProjectionMatrix();
        Vector3 lookDir = Vector3.Normalize(camPos - Position);

        string[] lines = Text.Split('\n');

        float maxLineWidth = 0f;
        List<float> lineWidths = new();
        foreach (string line in lines)
        {
            float lineWidth = 0f;
            foreach (char c in line)
            {
                if (_characters.TryGetValue(c, out var ch))
                    lineWidth += (ch.Advance >> 6) * _scale;
            }
            lineWidths.Add(lineWidth);
            maxLineWidth = Math.Max(maxLineWidth, lineWidth);
        }

        float lineHeight = 0f;
        foreach (var ch in _characters.Values)
            lineHeight = Math.Max(lineHeight, ch.Size.Y * _scale);

        float totalTextHeight = lines.Length * lineHeight;
        float verticalOffsetForCentering = totalTextHeight / 2f;

        Vector3 bgPos = Position - lookDir * 0.001f;
        float padX = 0.05f * maxLineWidth;
        float padY = 0.1f * totalTextHeight;

        float bgWidth = maxLineWidth + padX;
        float bgHeight = totalTextHeight + padY;
        Vector3 bgOffset = new Vector3(-bgWidth / 2f, bgHeight / 2f - 0.045f, 0f);

        var modelBG = Matrix4.CreateScale(bgWidth, bgHeight, 1f)
                      * Matrix4.CreateTranslation(bgOffset)
                      * Camera.Instance.GetBillboard(bgPos)
                      * Matrix4.CreateTranslation(bgPos)
                      * Matrix4.CreateTranslation(0, _bottomToTop ? (bgHeight / 2) : 0, 0);

        int locM = GL.GetUniformLocation(_shaderProgram, "uModel");
        int locV = GL.GetUniformLocation(_shaderProgram, "uView");
        int locP = GL.GetUniformLocation(_shaderProgram, "uProjection");
        int locC = GL.GetUniformLocation(_shaderProgram, "uColor");

        GL.UniformMatrix4(locM, false, ref modelBG);
        GL.UniformMatrix4(locV, false, ref view);
        GL.UniformMatrix4(locP, false, ref projection);
        GL.Uniform4(locC, _bgColor);

        GL.BindVertexArray(_vao);
        GL.BindTexture(TextureTarget.Texture2D, _whiteTexture);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 6);

        GL.Uniform4(locC, _textColor);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            float penX = 0f;
            float yrel = -i * lineHeight + verticalOffsetForCentering - lineHeight;

            float lineWidth = lineWidths[i];

            foreach (char c in line)
            {
                if (!_characters.TryGetValue(c, out var ch))
                    continue;

                float w = ch.Size.X * _scale;
                float h = ch.Size.Y * _scale;

                float xrel = penX + (ch.Bearing.X * _scale) - lineWidth / 2f;
                float yoff = yrel + (ch.Bearing.Y * _scale);

                var modelCh = Matrix4.CreateScale(w, h, 1f)
                                * Matrix4.CreateTranslation(xrel, yoff, 0f)
                                * Camera.Instance.GetBillboard(Position)
                                * Matrix4.CreateTranslation(Position)
                                * Matrix4.CreateTranslation(0, _bottomToTop ? (bgHeight / 2) : 0, 0);

                GL.UniformMatrix4(locM, false, ref modelCh);
                GL.BindTexture(TextureTarget.Texture2D, ch.TextureID);
                GL.DrawArrays(PrimitiveType.Triangles, 0, 6);

                penX += (ch.Advance >> 6) * _scale;
            }
        }

        GL.BindVertexArray(0);
        GL.BindTexture(TextureTarget.Texture2D, 0);
    }


    public override RenderLayer RenderLayer => RenderLayer.World;
    public override bool IsVisible { get; set; } = true;
    public override void Dispose()
    {
        this.StopRender();
        GL.DeleteVertexArray(_vao);
        GL.DeleteBuffer(_vbo);
        GL.DeleteProgram(_shaderProgram);
    }
}