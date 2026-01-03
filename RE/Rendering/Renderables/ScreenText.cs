using System.Diagnostics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core;
using RE.Core.Assets;
using RE.Rendering.Text;

namespace RE.Rendering.Renderables;

[DebuggerDisplay("Text: \"{Text}\"")]
internal class ScreenText : Renderable
{
    public ScreenText(string? content, Vector2 position, FreeTypeFont font)
    {
        Text = content ?? "";
        Position = position;
        Font = font;
    }

    public ScreenText(string? content, Vector2 position, FreeTypeFont font, float scale)
        : this(content, position, font)
    {
        Scale = scale;
    }

    public ScreenText(string? content, Vector2 position, FreeTypeFont font, Vector4 color)
        : this(content, position, font)
    {
        Color = color;
    }

    public ScreenText(string? content, Vector2 position, FreeTypeFont font, float scale, Vector4 color)
        : this(content, position, font)
    {
        Scale = scale;
        Color = color;
    }

    public ScreenText(string? content, Vector2 position, FreeTypeFont font, float scale, Vector4 color, Vector2 direction)
        : this(content, position, font, scale, color)
    {
        Direction = direction;
    }

    public string Text { get; set; }
    public Vector2 Position { get; set; }
    public float Scale { get; set; } = 1.0f;
    public Vector4 Color { get; set; } = new(0, 0, 0, 1);
    public FreeTypeFont Font { get; }
    public Vector2 Direction { get; set; } = new(1, 0);
    public override bool IsVisible { get; set; } = true;
    public override RenderLayer RenderLayer => RenderLayer.UI;

    private static ShaderProgram _shaderProgram = null!;

    public override void Render(FrameEventArgs args)
    {
        if (!IsVisible)
            return;

        if (_shaderProgram == null!)
        {
            _shaderProgram = new();
            _shaderProgram.AttachShader("assets/shaders/text.frag");
            _shaderProgram.AttachShader("assets/shaders/text.vert");
        }

        var projectionM = Matrix4.CreateOrthographicOffCenter(0.0f, Game.Instance.ClientSize.X,
            Game.Instance.ClientSize.Y, 0.0f, -1.0f, 1.0f);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(0, BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
        _shaderProgram.Use();
        GL.UniformMatrix4(1, false, ref projectionM);
        GL.Uniform4(3, Color);
        Font.RenderText(Text, Position.X, Position.Y, Scale, Direction);
    }
}