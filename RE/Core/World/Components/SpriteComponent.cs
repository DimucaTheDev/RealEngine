using System.Text.Json.Nodes;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Rendering.Renderables;

namespace RE.Core.World.Components
{
    internal class SpriteComponent(string path, float size = 0.25f) : Component
    {
        public SpriteComponent() : this("Assets/sprites/editor/blank.png") { }

        private SpriteRenderer _sprite = new SpriteRenderer(Vector3.Zero, path, scale: size, constantSize: false);

        public override void Update(FrameEventArgs args)
        {
            _sprite.Position = Owner.Transform.Position;
        }

        public override void Render(FrameEventArgs args)
        {
            _sprite.Render(args);
        }

        public override void OnDestroy()
        {
            _sprite.Dispose();
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
