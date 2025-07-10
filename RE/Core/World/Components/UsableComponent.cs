using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core.Scripting;
using RE.Debug;
using RE.Rendering.Renderables;

namespace RE.Core.World.Components
{
    internal class UsableComponent : Component, ISceneRenderer
    {
        private SpriteRenderer _spriteClick;

        public Action? OnUsed;
        public UsableComponent() { }
        public UsableComponent(string command) => OnUsed += () => CommandHandler.ExecuteCommand(command);

        [EditorProperty(IsReadOnly = true)] public string Subscribers => $"{OnUsed?.GetInvocationList().Length} Total";
        [EditorProperty(DisplayedName = "Invoke Command")] public string Command { get; set; }

        public override void Start()
        {
            _spriteClick = new SpriteRenderer(Vector3.Zero, "assets/sprites/editor/use.png", scale: .5f, constantSize: false);
            OnUsed += () => CommandHandler.ExecuteCommand(Command);
        }

        public void DebugRender(FrameEventArgs args)
        {
            _spriteClick.Position = Owner.Transform.Position + (0, Owner.Transform.Scale.Y * 2, 0);
            _spriteClick.Render(args);
        }
    }
}
