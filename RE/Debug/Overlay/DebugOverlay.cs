using OpenTK.Windowing.Common;
using RE.Core;
using RE.Libs.Grille.ImGuiTK;
using RE.Rendering;
using RE.Rendering.Text;
using static ImGuiNET.ImGui;

namespace RE.Debug.Overlay
{
    internal class DebugOverlay : IRenderable
    {
        public static DebugOverlay? Instance { get; private set; }
        public RenderLayer RenderLayer { get; } = RenderLayer.Overlay;
        public bool IsVisible { get; set; } = true;

        private readonly ImGuiController _controller;

        private DebugOverlay()
        {
            _controller = new ImGuiController();
            Game.Instance.RenderFrame += Render;
            Game.Instance.Resize += (args) => _controller.WindowResized(args.Width, args.Height);
            Game.Instance.MouseWheel += (args) => _controller.MouseScroll(args.Offset);
            Game.Instance.TextInput += (args) => _controller.PressChar((char)args.Unicode);
            RenderLayerManager.AddRenderable(this, typeof(DebugOverlay));
        }

        public static void Init()
        {
            Instance ??= new DebugOverlay();
        }

        public void Render(FrameEventArgs args)
        {
            _controller.Update(Game.Instance, Time.DeltaTime);

            Begin("123");
            if (Button("gc")) GC.Collect();
            if (Button("rem")) RenderLayerManager.RemoveRenderable(Game.Instance.renderable, typeof(Text));
            if (Button("add")) RenderLayerManager.AddRenderable(Game.Instance.renderable, typeof(Text));
            End();

            _controller.Render();
        }
    }
}
