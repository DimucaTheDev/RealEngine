using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Audio;
using RE.Core.World;
using RE.Core.World.Components;
using RE.Rendering;
using RE.Utils;
using Serilog;
using static ImGuiNET.ImGui;

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