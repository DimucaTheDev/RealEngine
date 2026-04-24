using OpenTK.Mathematics;
using RE.Rendering.Renderables;
using RE.Rendering.Text;

namespace RE.Core.Ui.Controls
{
    internal class LabelControl : UiControl
    {
        public ScreenText TextRenderer { get; }
        public string? Text { get; set; }

        public LabelControl()
        {
            TextRenderer = new();
            Scale = Vector2.One;
        }

        public LabelControl(ScreenText textRenderer)
        {
            TextRenderer = textRenderer;
            Scale = Vector2.One;
        }

        public override void Update(float delta)
        {
            TextRenderer.Position = Position;// + (0, TextRenderer.Font.PixelHeight);
            TextRenderer.Text = Text ?? "";
        }

        public override void Render()
        {
            TextRenderer.Render(default);
        }

        public override (Vector2 Position, Vector2 Scale) GetBoundary()
        { 
            return (Position - (0, TextRenderer.Font.GetTextHeight(Text!)), 1.25f * new Vector2(TextRenderer.Font.GetTextWidth(Text!), TextRenderer.Font.GetTextHeight(Text!)));
        }
    }
}
