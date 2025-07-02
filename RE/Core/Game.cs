using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.Desktop;
using RE.Audio;
using RE.Debug;
using RE.Debug.Overlay;
using RE.Libs.Grille.ImGuiTK;
using RE.Rendering;
using RE.Rendering.Skybox;
using RE.Utils;
using Serilog;
using Serilog.Events;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Camera = RE.Rendering.Camera;
using Image = OpenTK.Windowing.Common.Input.Image;
using TextRenderer = RE.Rendering.Text.TextRenderer;

namespace RE.Core;

internal class Game : GameWindow
{
    private Game(GameWindowSettings gws, NativeWindowSettings nws) : base(gws, nws) { }

    public static Game Instance { get; private set; }
    public static StringWriter GameLog = new();

    private static readonly Dictionary<nint, string> _loadedLibs = new();

    public static void Start()
    {
        Thread.CurrentThread.Name = "Render Thread";
        Environment.CurrentDirectory = AppContext.BaseDirectory;

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.WithThreadName()
            .WriteTo.TextWriter(GameLog, LogEventLevel.Information, outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{ThreadName}] {Message:lj}{NewLine}{Exception}")
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
            new GameWindowSettings { UpdateFrequency = 60 },
            new NativeWindowSettings
            {
                Title = "Real Engine",
                ClientSize = new Vector2i(1280, 720),
                Location = new Vector2i(Screen.PrimaryScreen!.Bounds.Width / 2 - 640, Screen.PrimaryScreen.Bounds.Height / 2 - 360),
                Icon = LoadIcon(),
                WindowState = WindowState.Normal
            });
        Instance = game;

        game.Run();

        Log.Information("End");
    }

    protected override void OnLoad()
    {
        UpdateFrame += Time.Update;
        UpdateFrame += SoundManager.Update;

        RenderLayerManager.Init();
        Time.Init();
        ImGuiController.Get();
        Camera.Init();
        TextRenderer.Init();
        Initializer.Init();

        Initializer.AddStep(("Initializing Debug Overlay", DebugOverlay.Init));
        Initializer.AddStep(("Initializing Debug Renderer", () =>
                {
                    LineManager.Main!.Init();
                    LineManager.Main.Render();
                }
        ));
        Initializer.AddStep(("Initializing ConsoleWindow", ConsoleWindow.Init));
        Initializer.AddStep(("Initializing Skybox", SkyboxRenderer.Init));
        Initializer.AddStep(("Initializing SoundManager", SoundManager.Init));
        Initializer.AddStep(("Adding stuff to scene...", () =>
                {
                    var startNew = Stopwatch.StartNew();

                    for (int i = 0; i < 1; i++)
                    {
                        //new ModelRenderer("Assets/Models/test.fbx").Render();
                    }

                    Log.Information($"Model loaded in {startNew.ElapsedMilliseconds} ms");
                }
        ));

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
        if (Initializer.Render(args))
            return;

        GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        GL.DepthFunc(DepthFunction.Lequal);
        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        GL.ClearColor(Color.CadetBlue);

        RenderLayerManager.RenderAll(args);

        base.OnRenderFrame(args);

        SwapBuffers();
    }

    public void ToggleFullscreen()
    {
        if (WindowState == WindowState.Fullscreen)
        {
            Log.Debug("Switching to windowed mode");
            WindowState = WindowState.Normal;
            WindowBorder = WindowBorder.Resizable;
        }
        else
        {
            Log.Debug("Switching to fullscreen mode");
            WindowState = WindowState.Fullscreen;
            WindowBorder = WindowBorder.Hidden;
        }
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
}