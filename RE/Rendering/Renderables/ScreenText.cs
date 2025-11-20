using System.Diagnostics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
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

    public override void Render(FrameEventArgs args)
    {
        if (!IsVisible)
            return;
        GL.Uniform4(3, Color);
        Font.RenderText(Text, Position.X, Position.Y, Scale, Direction);
    }
}