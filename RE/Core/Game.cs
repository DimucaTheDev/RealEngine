using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using RE.Debug.Overlay;
using RE.Rendering;
using RE.Rendering.Camera;
using RE.Rendering.Skybox;
using RE.Rendering.Text;
using RE.Utils;
using Serilog;
using System.Drawing;

namespace RE.Core;

internal class Game : GameWindow
{
    private Game(GameWindowSettings gws, NativeWindowSettings nws) : base(gws, nws) { }

    private readonly Dictionary<nint, string> _loadedLibs = new();

    public static Game Instance { get; private set; }

    public static void Start()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.WithThreadName()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] [{ThreadName}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
        Log.Information("Hello, World!");

        using var game = new Game(
            new GameWindowSettings { UpdateFrequency = 0 },
            new NativeWindowSettings { Title = "DaRealEngin", ClientSize = new Vector2i(800, 600) });
        Instance = game;
        Thread.CurrentThread.Name = "Render Thread";

        game.Run();

        Log.Information("End");
    }

    protected override void OnLoad()
    {
        foreach (var lib in Directory.GetFiles("Dll", "*.dll"))
        {
            nint handle;
            _loadedLibs.Add(handle = WinApi.LoadLibrary(lib), lib);
            Log.Debug($"Loaded DLL \"{lib}\": 0x{handle:X} ");
        }

        UpdateFrame += Time.Update;

        RenderLayerManager.Init();
        Time.Init();
        Camera.Init();
        TextRenderer.Init();
        DebugOverlay.Init();
        SkyboxRenderer.Init();

        RenderLayerManager.AddRenderable(Camera.l);

        base.OnLoad();
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        Camera.Instance.AspectRatio = (float)e.Width / e.Height;
        GL.Viewport(0, 0, e.Width, e.Height);
        base.OnResize(e);
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        GL.DepthFunc(DepthFunction.Lequal);
        GL.ClearColor(Color.CadetBlue);


        RenderLayerManager.RenderAll(args);


        base.OnRenderFrame(args);

        SwapBuffers();
    }

    protected override void OnUnload()
    {
        base.OnUnload();
        foreach (var lib in _loadedLibs)
        {
            Log.Information($"Unloading library \"{lib.Value}\"");
            WinApi.FreeLibrary(lib.Key);
        }
    }
}