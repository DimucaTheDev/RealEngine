using System.Text.Json.Nodes;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core.Scripting;
using RE.Core.Scripting.Attributes;
using RE.Rendering.Renderables;
using RE.Rendering.Text;

namespace RE.Core.World.Components
{
    [ComponentInfo("Text", Description = "Shows a text that always faces camera")]
    public class BillboardTextComponent(string text) : Component
    {
        private readonly FloatingText _text = new(text, Vector3.Zero, new FreeTypeFont(64, Fonts.Consolas));

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
            var posOffset = new JsonArray { PositionOffset.X, PositionOffset.Y, PositionOffset.Z };
            var args = new JsonArray { Text };
            root.Add("args", args);
            root.Add(nameof(PositionOffset), posOffset);
            return root;
        }
    }
}
