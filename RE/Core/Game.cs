using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.Desktop;
using RE.Debug.Overlay;
using RE.Libs.Grille.ImGuiTK;
using RE.Rendering;
using RE.Rendering.Camera;
using RE.Rendering.Skybox;
using RE.Rendering.Text;
using RE.Utils;
using Serilog;
using Serilog.Events;
using System.Drawing;
using System.Drawing.Imaging;
using Image = OpenTK.Windowing.Common.Input.Image;

namespace RE.Core;

internal class Game : GameWindow
{

    // ...

    public static WindowIcon? LoadIcon()
    {
        var path = "Assets/RealEngine.ico";
        if (!File.Exists(path))
        {
            Log.Warning($"Icon file not found: {path}");
            return null;
        }
        try
        {
            using var icon = new Icon(path); // Загружаем иконку
            using var bitmap = icon.ToBitmap(); // Преобразуем в Bitmap

            var data = new byte[bitmap.Width * bitmap.Height * 4];
            var bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            System.Runtime.InteropServices.Marshal.Copy(bitmapData.Scan0, data, 0, data.Length);
            bitmap.UnlockBits(bitmapData);

            // OpenTK требует RGBA, а System.Drawing выдает ARGB => нужно конвертировать
            for (int i = 0; i < data.Length; i += 4)
            {
                byte a = data[i + 3];
                byte r = data[i + 2];
                byte g = data[i + 1];
                byte b = data[i + 0];

                data[i + 0] = r;
                data[i + 1] = g;
                data[i + 2] = b;
                data[i + 3] = a;
            }

            var image = new Image(bitmap.Width, bitmap.Height, data);
            return new WindowIcon(image);
        }
        catch (Exception e)
        {
            Log.Error(e, "An error occurred while loading the icon.");
            throw;
        }
    }

    private Game(GameWindowSettings gws, NativeWindowSettings nws) : base(gws, nws) { }

    private static readonly Dictionary<nint, string> _loadedLibs = new();
    public static StringWriter stringWriter = new StringWriter();
    public static Game Instance { get; private set; }

    public static void Start()
    {
        Thread.CurrentThread.Name = "Render Thread";
        Environment.CurrentDirectory = AppContext.BaseDirectory;

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.WithThreadName()
            .WriteTo.TextWriter(stringWriter, LogEventLevel.Information, outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{ThreadName}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] [{ThreadName}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
        Log.Information("Hello, World!");

        foreach (var lib in Directory.GetFiles("Dll", "*.dll"))
        {
            nint handle;
            _loadedLibs.Add(handle = WinApi.LoadLibrary(lib), lib);
            Log.Debug($"Loaded DLL \"{Path.GetFileName(lib)}\": 0x{handle:X} ");
        }

        using var game = new Game(
            new GameWindowSettings { UpdateFrequency = 0 },
            new NativeWindowSettings
            {
                Title = "Real Engine",
                ClientSize = new Vector2i(800, 600),
                Icon = LoadIcon()
            });
        Instance = game;

        game.Run();

        Log.Information("End");
    }

    protected override void OnLoad()
    {
        UpdateFrame += Time.Update;

        RenderLayerManager.Init();
        Time.Init();
        Camera.Init();
        TextRenderer.Init();
        ImGuiController.Get();
        DebugOverlay.Init();
        Debug.Overlay.ConsoleWindow.Init();
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
            Log.Debug($"Unloading library \"{lib.Value}\"");
            WinApi.FreeLibrary(lib.Key);
        }
    }
}