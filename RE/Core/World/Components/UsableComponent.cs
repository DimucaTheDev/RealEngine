using System.Text.Json.Nodes;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core.Scripting;
using RE.Debug;
using RE.Rendering.Renderables;

namespace RE.Core.World.Components
{
    [ComponentInfo("World/Scripting", Description = "Triggers actions or commands when the player presses 'E' while looking at the object")]
    internal class UsableComponent : Component, ISceneRenderer
    {
        private SpriteRenderer _spriteClick;

        public Action? OnUsed;
        public UsableComponent() { }
        public UsableComponent(string command) => Command = command;

        [EditorProperty(IsReadOnly = true)] public string Subscribers => $"{OnUsed?.GetInvocationList().Length} Total";
        [EditorProperty(DisplayedName = "Invoke Command")] public string Command { get; set; }

        public override void Start()
        {
            _spriteClick = new SpriteRenderer(Vector3.Zero, "assets/sprites/editor/use.png");
            OnUsed += () => CommandHandler.ExecuteCommand(Command);
        }

        public void DebugRender(FrameEventArgs args)
        {
            if (_spriteClick! == null!)
                return;
            _spriteClick.Position = Owner.Transform.Position + (0, Owner.Transform.Scale.Y + 0.3f, 0);
            _spriteClick.Render(args);
        }
        public override JsonNode GetSaveData()
        {
            JsonObject root = new();
            if (Command != null!)
            {
                var args = new JsonArray();
                args.Add(Command);
                root.Add("args", args);
            }

            return root;
        }
    }
}
