using System.Text.Json.Nodes;
using JetBrains.Annotations;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core.Scripting.Attributes;
using RE.Editor;
using RE.Rendering.Renderables;
using RE.Rendering.Text;

namespace RE.Core.World.Components
{
    [ComponentInfo("Text", Description = "Shows a text that always faces camera")]
    public class BillboardTextComponent(string text) : Component, IEditorUpdate, IEditorRender
    {
        private readonly FloatingText _text = new(text, Vector3.Zero, new FreeTypeFont(Fonts.Consolas, 64));

        public BillboardTextComponent() : this("Billboard Text")
        {
        }

        [EditorProperty] public Vector3 PositionOffset { get; set; }

        [EditorProperty]
        public string Text
        {
            get => _text.Text;
            set => _text.Text = value;
        }

        public override bool IsOpaque => false;

        public override void Update(double args)
        {
            _text.Position = Owner.Transform.Position + PositionOffset;
        }

        public override void Render(double args)
        {
            _text.Render(args);
        }

        public override void Destroy()
        {
            _text.Dispose();
        }

        public override JsonObject GetSaveData()
        {
            JsonObject root = new();
            var posOffset = new JsonArray { PositionOffset.X, PositionOffset.Y, PositionOffset.Z };
            var args = new JsonArray { Text };
            root.Add("args", args);
            root.Add(nameof(PositionOffset), posOffset);
            return root;
        }

        /// <inheritdoc />
        public void EditorUpdate(double delta) => Update(delta);

        /// <inheritdoc />
        public void EditorRender(double delta) => Render(delta);
    }
}