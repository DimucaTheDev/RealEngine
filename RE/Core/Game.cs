using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.Desktop;
using RE.Audio;
using RE.Core.Assets;
using RE.Core.PluginSystem;
using RE.Core.Scripting;
using RE.Core.World.Physics;
using RE.Debug;
using RE.Debug.Overlay;
using RE.Libs.Grille.ImGuiTK;
using RE.Rendering;
using RE.Utils;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Camera = RE.Rendering.Camera;
using Color = System.Drawing.Color;
using Image = OpenTK.Windowing.Common.Input.Image;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;
using PixelFormat = OpenTK.Graphics.OpenGL.PixelFormat;
using Rectangle = System.Drawing.Rectangle;
using TextRenderer = RE.Rendering.Text.TextRenderer;

namespace RE.Core;

//todo: refactor this class into smaller parts
internal class Game : GameWindow
{
    private Game(GameWindowSettings gws, NativeWindowSettings nws) : base(gws, nws) { }

    public static Game Instance { get; private set; } = null!;
    public static readonly DateTime BuildDate =
        Assembly.GetExecutingAssembly().GetCustomAttribute<BuildDateAttribute>()?.DateTime.ToLocalTime() ??
        DateTime.MinValue;
    public static readonly string CommitHash = Assembly.GetExecutingAssembly().GetCustomAttribute<GitCommitAttribute>()?.CommitHash ?? "unknown";
    public const int FpsLock = 165; // fixme: fpsLock above 200 may cause physics issues
    public const string ProductName = "Real Engine";

    private static readonly Dictionary<nint, string> LoadedLibs = new();

    private static bool Wireframe
    {
        get => (bool)(Variables.GetVariable("wireframe") ?? false);
        set => Variables.SetVariable("wireframe", value);
    }
    public static void Start()
    {
        Thread.CurrentThread.Name = "Render Thread";
        Environment.CurrentDirectory = AppContext.BaseDirectory;

        SetupLogger();
        
        Log.Information("{ProductName}; build {BuildDate:dd.MM.yyyy HH:mm:ss}; commit {CommitHash}", ProductName, BuildDate, CommitHash[..7]);
        Log.Information("Startup args: {@Args}", Environment.GetCommandLineArgs()[1..]);

        foreach (var lib in Directory.GetFiles("Dll\\WIN32", "*.dll"))
        {
            nint handle;
            LoadedLibs.Add(handle = WinApi.LoadLibrary(lib), lib);
            var fName = Path.GetFileName(lib);
            var errCode = Marshal.GetLastWin32Error();

            if (errCode == 0)
                Log.Debug("Loaded DLL {FileName,-15}: 0x{Handle,-16:x} Code: {ErrorCode}", fName, handle, errCode);
            else
                Log.Debug("Loaded DLL {FileName,-15}: 0x{Handle,-16:x} Code: {ErrorCode}, {ErrorMessage}", fName, handle, errCode, Marshal.GetLastPInvokeErrorMessage());
        }

        PluginManager.ResolvePlugins();

        using var game = new Game(
            new GameWindowSettings { UpdateFrequency = FpsLock },
            new NativeWindowSettings
            {
                Title = $"{ProductName} - {BuildDate:g} - {CommitHash}",
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
        PhysicsManager.Update((float)args.Time);
        base.OnUpdateFrame(args);
    }

    protected override void OnLoad()
    {
        RenderManager.Init();
        Time.Init();
        ImGuiController.Get();
        Camera.Init();
        TextRenderer.Init();
        Initializer.Init();

        Initializer.AddStep(("Bootstrapping...", () =>
        {
            DebugOverlay.Init();
            LineManager.Main!.StartRender();
            ConsoleWindow.Init();
            SoundManager.Init();
            PhysicsManager.Init();
        }
        ));
        Initializer.AddStep(("Registering Commands", CommandHandler.RegisterAllCommands));
        Initializer.AddStep(("Running default.cfg", () => { CommandHandler.ExecuteCommand("source assets/cfg/default.cfg"); }
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
        {
            return;
        }

        GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        GL.DepthFunc(DepthFunction.Lequal);
        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.Blend);
        //GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.ClearColor(Color.CadetBlue);
        //GL.Enable(EnableCap.CullFace);
        //GL.CullFace(CullFaceMode.Back);
        //GL.FrontFace(FrontFaceDirection.Ccw);


        if (KeyboardState.IsKeyPressed(Keys.F3))
            Wireframe = !Wireframe;
        GL.PolygonMode(TriangleFace.FrontAndBack, Wireframe ? PolygonMode.Line : PolygonMode.Fill);

        base.OnRenderFrame(args);

        RenderManager.RenderAll(args);
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

        AssetManager.UnloadAll();
        PluginManager.UnloadPlugins();

        foreach (var lib in LoadedLibs)
        {
            Log.Debug("Unloading library {Library}", lib.Value);
            WinApi.FreeLibrary(lib.Key);
        }
    }
    public static string TakeScreenshot() => TakeScreenshot(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), ProductName,
        $"re_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png"));
    public static unsafe string TakeScreenshot(string fileName)
    {
        var a = new byte[Instance.ClientSize.X * Instance.ClientSize.Y * 3];
        fixed (byte* ptr = a)
            GL.ReadPixels(0, 0, Instance.ClientSize.X, Instance.ClientSize.Y, PixelFormat.Rgb, PixelType.UnsignedByte, (IntPtr)ptr);
        Image<Rgb24> image = SixLabors.ImageSharp.Image.LoadPixelData<Rgb24>(a, Instance.ClientSize.X, Instance.ClientSize.Y);
        image.Mutate(s => s.Flip(FlipMode.Vertical));

        Directory.CreateDirectory(Path.GetDirectoryName(fileName)!);

        image.SaveAsPng(fileName);
        image.Dispose();
        return Path.GetFullPath(fileName);
    }

    private static void SetupLogger()
    {
        string logTemplatePath = "Assets/logTemplate.txt";
        string[] args = Environment.GetCommandLineArgs();

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-log" && i + 1 < args.Length)
            {
                logTemplatePath = args[i + 1];
                break;
            }
        }
        var hasTemplate = File.Exists(logTemplatePath);
        var consoleTemplate = hasTemplate ? File.ReadAllText(logTemplatePath) :
            "[{Timestamp:HH:mm:ss.fff} {Level:u3}] [{ThreadName}] [{SourceContext:Name}] {Message:lj}{NewLine}{Exception}";

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.WithThreadName()
            .WriteTo.Sink(new GameLogger("[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"))
            .WriteTo.Console(outputTemplate: consoleTemplate)
            .CreateLogger()
            .ForContext("SourceContext", "Engine");

        Log.Information("Hello, World!");
        if (hasTemplate)
            Log.Information("Using log template from: {LogTemplatePath}", logTemplatePath);
    }
    private static WindowIcon? LoadIcon()
    {
        var path = "Assets/RealEngine2.ico";
        if (!File.Exists(path))
        {
            Log.Error("Icon file not found: {IconPath}", path);
            return null;
        }
        try
        {
            using var icon = new Icon(path);
            using var bitmap = icon.ToBitmap();

            var data = new byte[bitmap.Width * bitmap.Height * 4];
            var bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            System.Runtime.InteropServices.Marshal.Copy(bitmapData.Scan0, data, 0, data.Length);
            bitmap.UnlockBits(bitmapData);

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