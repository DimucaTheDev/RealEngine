using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using System.Diagnostics;

namespace RE.Rendering.Text
{
    [DebuggerDisplay($"Text: \"{nameof(Content)}\"")]
    internal class Text : IRenderable
    {
        public string Content { get; set; }
        public Vector2 Position { get; set; }
        public float Scale { get; set; } = 1.0f;
        public Vector4 Color { get; set; } = new(1, 1, 1, 1);
        public FreeTypeFont Font { get; private set; }
        public bool IsVisible { get; set; } = true;//TODO: move this to IRenderable interface
        public Vector2 Direction { get; set; } = new Vector2(1, 0);
        public RenderLayer RenderLayer => RenderLayer.UI;


        public Text(string content, Vector2 position, FreeTypeFont font)
        {
            Content = content;
            Position = position;
            Font = font;
        }
        public Text(string content, Vector2 position, FreeTypeFont font, float scale)
            : this(content, position, font)
        {
            Scale = scale;
        }
        public Text(string content, Vector2 position, FreeTypeFont font, Vector4 color)
            : this(content, position, font)
        {
            Color = color;
        }
        public Text(string content, Vector2 position, FreeTypeFont font, float scale, Vector4 color)
            : this(content, position, font)
        {
            Scale = scale;
            Color = color;
        }
        public Text(string content, Vector2 position, FreeTypeFont font, float scale, Vector4 color, Vector2 direction)
            : this(content, position, font, scale, color)
        {
            Direction = direction;
        }

        public void Render(FrameEventArgs args)
        {
            if (!IsVisible) return;
            GL.Uniform3(2, Color.Xyz);
            Font.RenderText(Content, Position.X, Position.Y, Scale, Direction);
        }
    }

}
