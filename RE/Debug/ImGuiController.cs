using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Backends.GLFW;
using Hexa.NET.ImGui.Backends.OpenGL3;
using Hexa.NET.ImGui.Backends.Win32;
using Hexa.NET.ImGuizmo;
using Hexa.NET.ImNodes;
using Hexa.NET.ImPlot;
using OpenTK.Graphics.OpenGL;
using RE.Core;
using RE.Core.Assets;
using RE.Utils;

namespace RE.Debug;

internal static class ImGuiController
{
    private static ImGuiContextPtr _context;
    private static ImPlotContextPtr _imPlotContext;
    private static ImNodesContextPtr _imNodesContext;

    /// <summary>
    ///     Initializes ImGui.
    /// </summary>
    public static unsafe void Init()
    {
        _context = ImGui.CreateContext();
        _imPlotContext = ImPlot.CreateContext();
        _imNodesContext = ImNodes.CreateContext();
        ImGui.SetCurrentContext(_context);
        
        // Setup ImGui config.
        var io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableGamepad;
        io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        io.ConfigFlags |= ImGuiConfigFlags.ViewportsEnable;


        if ((io.ConfigFlags & ImGuiConfigFlags.ViewportsEnable) != 0)
        {
            var style = ImGui.GetStyle();
            style.WindowRounding = 0.0f;
            style.Colors[(int)ImGuiCol.WindowBg].W = 1f;
        }

        ImGuiImplGLFW.SetCurrentContext(_context);
        if (!ImGuiImplGLFW.InitForOpenGL((GLFWwindow*)Game.Instance.WindowPtr, true))
            throw new Exception("Failed to init ImGui Impl GLFW");

        ImGuiImplOpenGL3.SetCurrentContext(_context);
        if (!ImGuiImplOpenGL3.Init("#version 150"))
            throw new Exception("Failed to init ImGui Impl OpenGL3");


        AddFont(ContentManager.GetBytes("assets/fonts/consola.ttf"),
            ImGui.ImFontConfig() with
            {
                FontDataOwnedByAtlas = false
            });
        AddFont(ContentManager.GetBytes("assets/fonts/icons.ttf"),
            ImGui.ImFontConfig() with
            {
                FontDataOwnedByAtlas = false,
                MergeMode = true,
                PixelSnapH = true
            }, [0xf000, 0xf950, 0]);

        ImGuiTheme.ApplyDarkTheme();
    }

    public static unsafe void AddFont(byte[] data, ImFontConfigPtr config, uint[]? ranges = null)
    {
        if (ranges != null)
        {
            fixed (void* fontData = data)
            fixed (uint* iconsRangesPtr = ranges)
                ImGui.GetIO().Fonts.AddFontFromMemoryTTF(fontData, data.Length, 13, config, iconsRangesPtr);
        }
        else
        {
            fixed (void* fontData = data)
                ImGui.GetIO().Fonts.AddFontFromMemoryTTF(fontData, data.Length, 13, config);
        }
    }

    /// <summary>
    ///     Renders the ImGui draw list data.
    /// </summary>
    public static void Render()
    {
        var io = ImGui.GetIO();

        ImGui.Render();

        Game.Instance.MakeCurrent();
        ImGuiImplOpenGL3.RenderDrawData(ImGui.GetDrawData());

        if ((io.ConfigFlags & ImGuiConfigFlags.ViewportsEnable) != 0)
        {
            ImGui.UpdatePlatformWindows();
            ImGui.RenderPlatformWindowsDefault();
        }

        Game.Instance.MakeCurrent();
    }

    public static void Update()
    {
        ImGui.SetCurrentContext(_context);
        ImGuizmo.SetImGuiContext(_context);
        ImPlot.SetImGuiContext(_context);
        ImNodes.SetImGuiContext(_context);
        ImPlot.SetCurrentContext(_imPlotContext);
        ImNodes.SetCurrentContext(_imNodesContext);
        ImGuiImplGLFW.SetCurrentContext(_context);
        ImGuiImplOpenGL3.SetCurrentContext(_context);

        ImGuiImplOpenGL3.NewFrame();
        ImGuiImplGLFW.NewFrame();
        ImGui.NewFrame();
        ImGuizmo.BeginFrame();

        ImGuizmo.SetRect(0, 0, Game.Instance.ClientSize.X, Game.Instance.ClientSize.Y);
    }
}