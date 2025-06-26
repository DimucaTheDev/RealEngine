using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Rendering;
using RE.Rendering.Camera;
using static ImGuiNET.ImGui;

namespace RE.Debug.Overlay;

internal class DebugOverlay : IRenderable
{

    private DebugOverlay()
    {
        RenderLayerManager.AddRenderable(this);
    }

    public static DebugOverlay? Instance { get; private set; }
    public RenderLayer RenderLayer => RenderLayer.ImGui;
    public bool IsVisible { get; set; } = true;

    public void Render(FrameEventArgs args)
    {

        Begin("123");
        if (Button("gc")) GC.Collect();
        var instance = Camera.Instance;
        Text($"Cam pos: ({instance.Position.X:F}; {instance.Position.Y:F}; {instance.Position.Z:F})");
        if (Button("1")) RenderLayerManager.RemoveRenderables<LineManager>();
        //Text($"a: ({LineManager.a.X:F}, {LineManager.a.Y:F})");

        if (Button("do_shit()"))
        {
            var start = Vector3.Zero;
            for (var i = 0; i < 50000; i++)
            {
                var end = start + new Vector3(Random.Shared.NextSingle() * .1f, Random.Shared.NextSingle() * .1f,
                    Random.Shared.NextSingle() * .1f);
                Camera.l.AddLine(start, end, new Vector4(1, 0, 0, 1), new Vector4(0, 0, 1, 1), 2000);
                start = end;
            }
        }

        //Text((1 / args.Time).ToString("F2") + " FPS\n" +
        //     $"DeltaTime: {Time.DeltaTime:F3} s\n" +
        //     $"Time: {Time.ElapsedTime:F3} s\n" +
        //     $"Camera Position: {Camera.Instance.Position.X:F2}, {Camera.Instance.Position.Y:F2}, {Camera.Instance.Position.Z:F2}" +
        //     $"\nAverage 5sFPS: {Game.A:F2}");
        End();
    }

    public static void Init()
    {
        Instance ??= new DebugOverlay();
    }
}