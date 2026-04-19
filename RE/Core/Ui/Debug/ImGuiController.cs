using System.Runtime.InteropServices;
using System.Text;
using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Backends.GLFW;
using Hexa.NET.ImGui.Backends.OpenGL3;
using Hexa.NET.ImGuizmo;
using Hexa.NET.ImNodes;
using Hexa.NET.ImPlot;
using RE.Core.Assets;
using RE.Utils;

namespace RE.Core.Ui.Debug;

internal static class ImGuiController
{
    private static ImGuiContextPtr _context;
    private static ImPlotContextPtr _imPlotContext;
    private static ImNodesContextPtr _imNodesContext;
    private static IntPtr _iniPtr = IntPtr.Zero;

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

        // set imgui.ini path
        byte[] utf8 = Encoding.UTF8.GetBytes("Engine/Config/imgui.ini" + "\0");
        _iniPtr = Marshal.AllocHGlobal(utf8.Length);
        Marshal.Copy(utf8, 0, _iniPtr, utf8.Length);
        io.IniFilename = (byte*)_iniPtr;
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
    public static unsafe void Render()
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

        var platformIO = ImGui.GetPlatformIO();

        IntPtr mainHwnd = (IntPtr)platformIO.Viewports[0].PlatformHandleRaw;

        for (int i = 1; i < platformIO.Viewports.Size; i++)
        {
            var vp = platformIO.Viewports[i];

            IntPtr childHwnd = (IntPtr)vp.PlatformHandleRaw;
            if (childHwnd == IntPtr.Zero || childHwnd == mainHwnd)
                continue;

            WinApi.HideFromTaskbar(childHwnd, mainHwnd);
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

    public static void Destroy()
    {
        ImGuiImplOpenGL3.Shutdown();
        ImGuiImplGLFW.Shutdown();
       // ImGuiImplWin32.Shutdown();
        
        ImPlot.DestroyContext(_imPlotContext);
        ImNodes.DestroyContext(_imNodesContext);
        ImGui.DestroyContext(_context);
    }
}