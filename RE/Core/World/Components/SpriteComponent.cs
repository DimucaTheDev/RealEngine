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

        public override bool IsOpaque => true;

        [EditorProperty] public float Scale { get => Sprite.Scale; set => Sprite.Scale = value; }

        public override void Update(double delta)
        {
            Sprite.Position = Owner.Transform.Position;
        }

        public override void Render(double args)
        {
            Sprite.Render(args);
        }

        public override void Destroy()
        {
            Sprite.Dispose();
        }

        public override JsonObject GetSaveData()
        {
            JsonObject root = new();
            var args = new JsonArray { path };
            root.Add("args", args);
            return root;
        }

        public override CollisionShape GetObjectSelectionShape() => new BoxShape(0.3f);

        /// <inheritdoc />
        public void EditorUpdate(double delta) => Update(delta);

        /// <inheritdoc />
        public void EditorRender(double delta) => Render(delta);
    }
}
