using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.Desktop;
using RE.Audio;
using RE.Core.Physics;
using RE.Debug;
using RE.Debug.Overlay;
using RE.Libs.Grille.ImGuiTK;
using RE.Rendering;
using RE.Rendering.Renderables;
using RE.Rendering.Text;
using RE.Utils;
using Serilog;
using Serilog.Events;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Camera = RE.Rendering.Camera;
using Color = System.Drawing.Color;
using Image = OpenTK.Windowing.Common.Input.Image;
using PixelFormat = OpenTK.Graphics.OpenGL.PixelFormat;
using Rectangle = System.Drawing.Rectangle;
using TextRenderer = RE.Rendering.Text.TextRenderer;
using Vector3 = BulletSharp.Math.Vector3;

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

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        PhysManager.Update((float)args.Time);
        base.OnUpdateFrame(args);
    }

    protected override void OnLoad()
    {
        UpdateFrame += Time.Update;
        UpdateFrame += SoundManager.Update;

        RenderManager.Init();
        Time.Init();
        ImGuiController.Get();
        Camera.Init();
        TextRenderer.Init();
        Initializer.Init();

        PhysManager.Init();
        PhysManager.c(new Vector3(10, 1, 10)).Render();

        new FloatingText("Adding physics cost me nerve cells\nIn fact, i tried 2 different libs!", new(-5, 5, 4), new FreeTypeFont(64, "assets/fonts/arial.ttf")).Render();

        OpenTK.Mathematics.Vector3 pyramidBaseCenter = new OpenTK.Mathematics.Vector3(0, 0, 0); // Adjust as needed
        float cubeSize = .45f;
        for (int k = 0; k < 1; k++)
        {
            for (int i = 0; i < 1; i++)
            {
                for (int j = 0; j < 0; j++)
                {
                    PhysManager.CreateCubePhysicsObject(
                        new ModelRenderer("assets/models/cub.fbx", new(j, i, k), scale: new(cubeSize, cubeSize, cubeSize)), 0.1f).Render();
                }
            }
        }

        Initializer.AddStep(("Initializing Debug Overlay", DebugOverlay.Init));
        Initializer.AddStep(("Initializing Debug Renderer", () =>
                {
                    // LineManager.Main!.Init();
                    LineManager.Main.Render();
                }
        ));
        Initializer.AddStep(("Initializing ConsoleWindow", ConsoleWindow.Init));
        Initializer.AddStep(("Initializing Skybox", SkyboxRenderer.Init));
        Initializer.AddStep(("Initializing SoundManager", SoundManager.Init));


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

        //LineManager.Main.Clear();
        GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        GL.DepthFunc(DepthFunction.Lequal);
        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.ClearColor(Color.CadetBlue);
        //GL.Enable(EnableCap.CullFace);
        //GL.CullFace(CullFaceMode.Back);
        //GL.FrontFace(FrontFaceDirection.Ccw);



        RenderManager.RenderAll(args);

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
    public static string TakeScreenshot() => TakeScreenshot($"re_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");
    public static unsafe string TakeScreenshot(string fileName)
    {
        byte[] a = new byte[Game.Instance.ClientSize.X * Game.Instance.ClientSize.Y * 3];
        fixed (byte* ptr = a)
            GL.ReadPixels(0, 0, Instance.ClientSize.X, Instance.ClientSize.Y, PixelFormat.Rgb, PixelType.UnsignedByte, (IntPtr)ptr);
        Image<Rgb24> image = SixLabors.ImageSharp.Image.LoadPixelData<Rgb24>(a, Game.Instance.ClientSize.X, Game.Instance.ClientSize.Y);
        image.Mutate(s => s.Flip(FlipMode.Vertical));
        image.SaveAsPng(fileName);
        image.Dispose();
        return Path.GetFullPath(fileName);
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