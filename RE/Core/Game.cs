using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using RE.Debug.Overlay;
using RE.Rendering;
using RE.Rendering.Camera;
using RE.Rendering.Skybox;
using RE.Rendering.Text;
using System.Drawing;
using System.Runtime.InteropServices;

namespace RE.Core;

internal class Game : GameWindow
{
    public static double A;

    private FreeTypeFont _font;
    private readonly Dictionary<nint, string> _loadedLibs = new();

    public Text renderable;


    private Game(GameWindowSettings gws, NativeWindowSettings nws) : base(gws, nws)
    {
    }

    public static Game Instance { get; private set; }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern void FreeLibrary(nint handle);

    public static void Start()
    {
        using var game = new Game(
            new GameWindowSettings { UpdateFrequency = 0 },
            new NativeWindowSettings { Title = "DaRealEngin", ClientSize = new Vector2i(800, 600) });
        Instance = game;
        Thread.CurrentThread.Name = "Render Thread";
        game.Run();
    }

    protected override void OnLoad()
    {
        foreach (var lib in Directory.GetFiles("Dll", "*.dll"))
        {
            nint handle;
            Console.Write($"Loading DLL \"{lib}\"... ");
            _loadedLibs.Add(handle = LoadLibrary(lib), lib);
            Console.WriteLine($"0x{handle:X}");
        }

        UpdateFrame += Time.Update;

        RenderLayerManager.Init();
        Time.Init();
        Camera.Init();
        TextRenderer.Init();
        DebugOverlay.Init();
        SkyboxRenderer.Init();
        RenderLayerManager.AddRenderable(Camera.l, typeof(RenderLayerManager));

        renderable = new Text("", new Vector2(20, 40), new FreeTypeFont(20, "c:/windows/fonts/arial.ttf"),
            new Vector4(1));
        RenderLayerManager.AddRenderable(renderable, typeof(Text));

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
            Console.WriteLine($"Unloading library \"{lib.Value}\"");
            FreeLibrary(lib.Key);
        }
    }
}