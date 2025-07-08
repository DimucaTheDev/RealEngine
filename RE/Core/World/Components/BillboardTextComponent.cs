using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Rendering.Renderables;
using RE.Rendering.Text;

namespace RE.Core.World.Components
{
    internal class BillboardTextComponent(string text) : Component
    {
        private FloatingText _text = new(text, Vector3.Zero, new FreeTypeFont(64, "assets/fonts/consola.ttf"));
        public BillboardTextComponent() : this("Billboard Text") { }

        public override void Update(FrameEventArgs args)
        {
            _text.Position = Owner.Transform.Position;
        }

        public override void Render(FrameEventArgs args)
        {
            _text.Render(args);
        }

        public override void OnDestroy()
        {
            _text.Dispose();
        }
    }
}
