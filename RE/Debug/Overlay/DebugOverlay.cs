using OpenTK.Windowing.Common;
using RE.Rendering;

namespace RE.Debug.Overlay;

internal class DebugOverlay : Renderable
{

    private DebugOverlay()
    {
        // RenderManager.AddRenderable(this);
    }

    public static DebugOverlay? Instance { get; private set; }
    public override RenderLayer RenderLayer => RenderLayer.ImGui;
    public override bool IsVisible { get; set; } = true;

    public override void Render(FrameEventArgs args) { }
    public static void Init()
    {
        Instance ??= new DebugOverlay();
    }
}