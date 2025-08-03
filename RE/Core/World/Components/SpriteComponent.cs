using System.Text.Json.Nodes;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core.Scripting;
using RE.Rendering.Renderables;

namespace RE.Core.World.Components
{
    [ComponentInfo("World", Description = "Renders 2D sprite in world that")]
    internal class SpriteComponent(string path, float size = 0.25f) : Component
    {
        public SpriteComponent() : this("Assets/sprites/editor/blank.png") { }

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
            var args = new JsonArray();
            args.Add(path);
            root.Add("args", args);
            return root;
        }
    }
}
