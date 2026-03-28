using System.Text.Json.Nodes;
using BulletSharp;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core.Scripting.Attributes;
using RE.Editor;
using RE.Rendering.Renderables;

namespace RE.Core.World.Components
{
    [ComponentInfo("World", Description = "Renders 2D sprite in world that")]
    internal class SpriteComponent(string path, float size = 0.25f) : Component, IEditorUpdate, IEditorRender
    {
        public SpriteComponent() : this("assets/editor/sprites/blank.png") { }
        public SpriteComponent(string path) : this(path, 0.25f) { }

        public readonly SpriteRenderer Sprite = new(Vector3.Zero, path, scale: size, constantSize: false);

        [EditorProperty] public float Scale { get => Sprite.Scale; set => Sprite.Scale = value; }

        public override void Update(FrameEventArgs args)
        {
            Sprite.Position = Owner.Transform.Position;
        }

        public override void Render(FrameEventArgs args)
        {
            Sprite.Render(args);
        }

        public override void OnDestroy()
        {
            Sprite.Dispose();
        }

        public override JsonNode GetSaveData()
        {
            JsonObject root = new();
            var args = new JsonArray { path };
            root.Add("args", args);
            return root;
        }

        public override CollisionShape GetObjectSelectionShape() => new BoxShape(0.3f);

        /// <inheritdoc />
        public void EditorUpdate(FrameEventArgs args) => Update(args);

        /// <inheritdoc />
        public void EditorRender(FrameEventArgs args) => Render(args);
    }
}
