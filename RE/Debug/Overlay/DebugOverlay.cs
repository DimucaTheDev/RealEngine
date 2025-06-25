using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core;
using RE.Libs.Grille.ImGuiTK;
using RE.Rendering;
using RE.Rendering.Camera;
using RE.Rendering.Text;
using static ImGuiNET.ImGui;

namespace RE.Debug.Overlay
{
    internal class DebugOverlay : IRenderable
    {
        public static DebugOverlay? Instance { get; private set; }
        public RenderLayer RenderLayer => RenderLayer.Overlay;
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
            var instance = Camera.Instance;
            Text($"Cam pos: ({instance.Position.X:F}; {instance.Position.Y:F}; {instance.Position.Z:F})");
            if (Button("1")) RenderLayerManager.RemoveRenderables(typeof(LineManager));
            //Text($"a: ({LineManager.a.X:F}, {LineManager.a.Y:F})");

            if (Button("do_shit()"))
            {
                Vector3 start = Vector3.Zero;
                for (int i = 0; i < 3000; i++)
                {
                    var end = start + new Vector3(Random.Shared.Next(-2, 2), Random.Shared.Next(-2, 2), Random.Shared.Next(-2, 2));
                    Camera.l.AddLine(start, end, new Vector4(1, 0, 0, 1), new Vector4(0, 0, 1, 1));
                    start = end;
                }
            }
            Text((1 / args.Time).ToString("F2") + " FPS\n" +
                 $"DeltaTime: {Time.DeltaTime:F3} s\n" +
                 $"Time: {Time.ElapsedTime:F3} s\n" +
                 $"Camera Position: {Camera.Instance.Position.X:F2}, {Camera.Instance.Position.Y:F2}, {Camera.Instance.Position.Z:F2}" +
                 $"\nAverage 5sFPS: {Game.A:F2}");
            End();

            _controller.Render();
        }
    }
}
