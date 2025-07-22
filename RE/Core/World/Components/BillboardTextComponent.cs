using System.Text.Json.Nodes;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core.Scripting;
using RE.Rendering.Renderables;
using RE.Rendering.Text;

namespace RE.Core.World.Components
{
    [ComponentInfo("Text", Description = "Shows a text that always faces camera")]
    internal class BillboardTextComponent(string text) : Component
    {
        private FloatingText _text = new(text, Vector3.Zero, new FreeTypeFont(64, "assets/fonts/consola.ttf"));
        public BillboardTextComponent() : this("Billboard Text") { }

        [EditorProperty] public Vector3 PositionOffset { get; set; }
        [EditorProperty] public string Text { get => _text.Text; set => _text.Text = value; }

        public override void Update(FrameEventArgs args)
        {
            _text.Position = Owner.Transform.Position + PositionOffset;
        }

        public override void Render(FrameEventArgs args)
        {
            _text.Render(args);
        }

        public override void OnDestroy()
        {
            _text.Dispose();
        }

        public override JsonNode GetSaveData()
        {
            JsonObject root = new();
            var args = new JsonArray();
            args.Add(Text);
            root.Add("args", args);
            return root;
        }
    }
}
