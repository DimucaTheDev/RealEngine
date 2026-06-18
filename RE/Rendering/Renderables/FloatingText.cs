using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core;
using RE.Core.Assets;
using RE.Rendering.Text;
using RE.Rendering.Texturing;
using RE.Utils;

namespace RE.Rendering.Renderables;

public class FloatingText : Renderable
{
    private readonly Dictionary<uint, Character> _characters;
    private readonly int _vao;
    private readonly int _vbo;
    private readonly ShaderProgram _shaderProgram;
    private readonly StaticTexture _whiteStaticTexture;
    private readonly bool _bottomToTop;
    private float _scale => Scale * 0.005f;

    private static FloatingText _instance;

    public Color4 BackgroundColor { get; set; }
    public Color4 ForegroundColor { get; set; }
    public override bool IsVisible { get; set; } = true;
    public Vector3 Position { get; set; }
    public float Scale { get; set; }
    public string Text { get; set; }

    public FloatingText(string content, Vector3 pos, FreeTypeFont font, bool bottomToTop = false)
        : this(content, pos, font, 1, Color4.White, new(0.3f, 0.3f, 0.3f, .5f), bottomToTop)
    {
    }

    public FloatingText(string content, Vector3 pos, FreeTypeFont font, float scale, Color4 foregroundColor,
        Color4 backgroundColor, bool bottomToTop)
    {
        Position = pos;
        Text = content;
        Scale = scale;

        _bottomToTop = bottomToTop;
        ForegroundColor = foregroundColor;
        BackgroundColor = backgroundColor;
        _characters = font.CharacterMap.ToDictionary();

        _shaderProgram = new ShaderProgram();
        _shaderProgram.AttachShader("Assets/Shaders/Pass/Ui/text_3d.vert");
        _shaderProgram.AttachShader("Assets/Shaders/Pass/Ui/text_3d.frag");


        float[] vertices =
        {
            //  x,     y,   u, v
            0.0f, -1.0f, 0.0f, 1.0f,
            0.0f, 0.0f, 0.0f, 0.0f,
            1.0f, 0.0f, 1.0f, 0.0f,

            0.0f, -1.0f, 0.0f, 1.0f,
            1.0f, 0.0f, 1.0f, 0.0f,
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

        _whiteStaticTexture = StaticTexture.CreateMonoColorTexture(Vector3.One);
    }

    public static void Render(string text, Vector3 pos)
    {
        if (_instance == null!)
        {
            _instance = new FloatingText("", Vector3.Zero, new FreeTypeFont(Fonts.Default));
        }

        _instance.Text = text;
        _instance.Position = pos;
        _instance.Render(new FrameEventArgs(Time.DeltaTime));
    }

    public override void Render(FrameEventArgs args)
    {
        if (string.IsNullOrEmpty(Text))
            return;

        _shaderProgram.Use();

        //fixme: "nuck z-buffer" ahh gl 🙏😭

        var camPos = Camera.GetActiveCamera().Position;
        var view = Camera.GetActiveCamera().GetViewMatrix();
        var projection = Camera.GetActiveCamera().GetProjectionMatrix();
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

        var modelBg = Matrix4.CreateScale(bgWidth, bgHeight, 1f)
                      * Matrix4.CreateTranslation(bgOffset)
                      * Camera.GetActiveCamera().GetBillboard(bgPos)
                      * Matrix4.CreateTranslation(bgPos)
                      * Matrix4.CreateTranslation(0, _bottomToTop ? bgHeight / 2 : 0, 0);

        _shaderProgram.SetValue("uModel", modelBg);
        _shaderProgram.SetValue("uView", view);
        _shaderProgram.SetValue("uProjection", projection);
        _shaderProgram.SetValue("uColor", BackgroundColor);

        //GL.ActiveTexture(TextureUnit.Texture2);
        //GL.BindTexture(TextureTarget.Texture2D, Game.Instance.OitDepthTexture);
        //GL.Uniform1(GL.GetUniformLocation(_shaderProgram.Handle, "uDepthTex"), 2); // указываем слот в шейдере


        GL.BindVertexArray(_vao);
        GL.BindTexture(TextureTarget.Texture2D, _whiteStaticTexture.AsOpenGl());
        GL.DrawArrays(PrimitiveType.Triangles, 0, 6);

        _shaderProgram.SetValue("uColor", ForegroundColor);
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

                float xrel = penX + ch.Bearing.X * _scale - lineWidth / 2f;
                float yoff = yrel + ch.Bearing.Y * _scale;

                var modelCh = Matrix4.CreateScale(w, h, 1f)
                              * Matrix4.CreateTranslation(xrel, yoff, 0f)
                              * Camera.GetActiveCamera().GetBillboard(Position)
                              * Matrix4.CreateTranslation(Position)
                              * Matrix4.CreateTranslation(0, _bottomToTop ? bgHeight / 2 : 0, 0);

                _shaderProgram.SetValue("uModel", modelCh);

                GL.BindTexture(TextureTarget.Texture2D, ch.TextureID);
                GL.DrawArrays(PrimitiveType.Triangles, 0, 6);

                penX += (ch.Advance >> 6) * _scale;
            }
        }

        GL.BindVertexArray(0);
        GL.BindTexture(TextureTarget.Texture2D, 0);
    }

    public override void Dispose()
    {
        this.StopRender();
        GL.DeleteVertexArray(_vao);
        GL.DeleteBuffer(_vbo);
        _shaderProgram.Delete();
    }
}