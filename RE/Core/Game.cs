using System.CommandLine;
using System.ComponentModel;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using RE.Audio;
using RE.Core.Assets;
using RE.Core.Assets.Providers;
using RE.Core.PluginSystem;
using RE.Core.Scripting;
using RE.Core.World.Physics;
using RE.Debug;
using RE.Debug.Overlay; 
using RE.Editor.Panels.Viewport;
using RE.External.Grille.ImGuiTK;
using RE.Rendering;
using RE.Utils;
using RenderdocSharp;
using Serilog;
using Camera = RE.Rendering.Camera;
using Color = System.Drawing.Color;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;
using SceneEditor = RE.Editor.SceneEditor;
using TextRenderer = RE.Rendering.Text.TextRenderer;

namespace RE.Core;

//todo: refactor this class into smaller parts
internal partial class Game : GameWindow
{
    public static ParseResult CommandParseResult = null!;
    public const int FpsLock = 165; // fixme: fpsLock above 200 may (and will) cause physics issues
     
    public static void Start(string[] args)
    {  
        Thread.CurrentThread.Name = "Render Thread";
        Environment.CurrentDirectory = AppContext.BaseDirectory;

        ParseArguments(args);
        SetupLogger();

        Log.Information("{ProductName}; build {BuildDate:dd.MM.yyyy HH:mm:ss}; commit {CommitHash}", ProductName, BuildDate, CommitHash[..7]);
        Log.Information("Startup args: {@Args}", args);

        if (Directory.Exists("Debug"))
            Directory.Delete("Debug", true);

        var nativesPath = CommandParseResult.GetValue<string>("--natives-path")!;

        foreach (var lib in Directory.GetFiles(nativesPath, "*.dll"))
        {
            nint handle;
            LoadedLibs.TryAdd(handle = WinApi.LoadLibrary(lib), lib);
            var fName = Path.GetFileName(lib);
            var errCode = Marshal.GetLastWin32Error();

            if (errCode == 0)
                Log.Debug("Loaded DLL {FileName,-15}: 0x{Handle,-16:x} Code: {ErrorCode}", fName, handle, errCode);
            else
                Log.Debug("Error loading DLL {FileName,-15}: {ErrorCode}, {ErrorMessage}", fName, errCode, Marshal.GetLastPInvokeErrorMessage());
        }

        PluginManager.ResolvePlugins();

        var width = CommandParseResult.GetValue<int>("--width");
        var height = CommandParseResult.GetValue<int>("--height");

        using var game = new Game(
            new GameWindowSettings { UpdateFrequency = FpsLock },
            new NativeWindowSettings
            {
                Title = $"{ProductName} - {BuildDate:g} - {CommitHash}",
                ClientSize = new Vector2i(width, height),
                Location = new Vector2i(Screen.PrimaryScreen!.Bounds.Width / 2 - width / 2, Screen.PrimaryScreen.Bounds.Height / 2 - height / 2),
                WindowState = WindowState.Normal
            });
        Instance = game;

        Instance.JoystickConnected += static e =>
        {
            if (e.IsConnected)
            {
                Log.Information("Joystick connected: {Name} ({JoystickId})", Instance.JoystickStates[e.JoystickId].Name,
                    e.JoystickId);
                JoystickNames[e.JoystickId] = Instance.JoystickStates[e.JoystickId].Name;
            }
            else
            {
                Log.Information("Joystick disconnected: {Name} ({JoystickId})", JoystickNames[e.JoystickId], e.JoystickId);
                JoystickNames.Remove(e.JoystickId);
            }
        };

        if (Renderdoc.IsAvailable)
            Log.Information("RenderDoc available: {Version}", Renderdoc.Version);

        game.Run();

        Log.Information("End");
    }

    protected override void OnLoad()
    {
        GL.Enable(EnableCap.DebugOutput);
        GL.Enable(EnableCap.DebugOutputSynchronous);
        GL.DebugMessageCallback(GlLogCallback, 0);

        ContentManager.Register(new FileContentProvider());
        ContentManager.Register(new ZipContentProvider());

        Icon = LoadIcon();

        RenderManager.Init();
        Time.Init();
        ImGuiController.Get();
        Camera.Init();
        TextRenderer.Init();
        Initializer.Init();

        Initializer.AddStep(("Bootstrapping...", () =>
                {
                    DebugOverlay.Init();
                    LineRenderer.Main!.StartRender();
                    ConsoleWindow.Init();
                    SoundManager.Init();
                    PhysicsManager.Init();
                }
        ));
        Initializer.AddStep(("Initializing Scene Editor...", () =>
                {
                    SceneEditor.Instance = new();
                }
        ));
        Initializer.AddStep(("Registering Commands", CommandHandler.RegisterAllCommands));
        Initializer.AddStep(("Running default.cfg", () => CommandHandler.ExecuteCommand("source assets/cfg/default.cfg")));
        base.OnLoad();
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        PhysicsManager.Update((float)args.Time);

        if (SceneEditor.Enabled)
            ConsoleWindow.Instance!.IsVisible = false;

        if (KeyboardState.IsKeyPressed(Keys.GraveAccent) && !SceneEditor.Enabled)
        {
            if (ConsoleWindow.Instance!.IsVisible)
            {
                ConsoleWindow.Instance!.IsVisible = false;
                Game.Instance.CursorState = CursorState.Grabbed;
            }
            else
            {
                ConsoleWindow.Instance!.IsVisible = true;
                Game.Instance.CursorState = CursorState.Normal;
                Camera.GetActiveCamera().FirstMove = true;
            }
        }

        base.OnUpdateFrame(args);
    }
    protected override void OnResize(ResizeEventArgs e)
    {
        Camera.Main.RenderWidth = SceneEditor.Enabled ? (int)ViewportPanel.ViewportSize.X : e.Width;
        Camera.Main.RenderHeight = SceneEditor.Enabled ? (int)ViewportPanel.ViewportSize.Y : e.Height;

        var w = Camera.Main.RenderWidth;
        var h = Camera.Main.RenderHeight;
        GL.Viewport(0, 0, w, h);
        SetupSceneFbo(w, h);
        base.OnResize(e);
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        RenderProfiler.StopAll();
        RenderProfiler.StartNew("render");

        if (Initializer.Render(args))
        {
            return;
        }

        #region GL
        GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        GL.DepthFunc(DepthFunction.Lequal);
        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.Blend);
        GL.ClearColor(Color.CadetBlue);
        GL.PolygonMode(TriangleFace.FrontAndBack, Wireframe ? PolygonMode.Line : PolygonMode.Fill);
        #endregion

        ImGuiController.Get().Update(this, Time.DeltaTime);

        if (SceneEditor.Enabled)
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, SceneFboId);

        var w = Camera.GetActiveCamera().RenderWidth;
        var h = Camera.GetActiveCamera().RenderHeight;
        GL.Viewport(0, 0, w, h);

        GL.ClearColor(Color.Black);//CadetBlue
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        GL.DepthFunc(DepthFunction.Lequal);
        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.Blend);
        GL.PolygonMode(TriangleFace.FrontAndBack, Wireframe ? PolygonMode.Line : PolygonMode.Fill);

        RenderManager.RenderAll(args);

        if (SceneEditor.Enabled)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.ClearColor(Color4.Black);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
        }

        ImGuiController.Get().Render();

        SwapBuffers();

        #region Keybinds
        if (KeyboardState.IsKeyPressed(Keys.F3))
            Wireframe = !Wireframe;
        if (KeyboardState.IsKeyPressed(Keys.F11))
            Game.Instance.ToggleFullscreen();
        if (KeyboardState.IsKeyPressed(Keys.F4))
            CommandHandler.ExecuteCommand("editor");
        if (KeyboardState.IsKeyPressed(Keys.F5))
            CommandHandler.ExecuteCommand("debug");
        if (KeyboardState.IsKeyPressed(Keys.F1))
        {
            RenderManager.RemoveCameraFrustum();
            if (KeyboardState.IsKeyDown(Keys.LeftControl))
            {
                return;
            }

            RenderManager.CreateCameraFrustum();
        }
        if (KeyboardState.IsKeyPressed(Keys.F2))
        {
            var p = Game.TakeScreenshot();
            Log.Information("Screenshot saved to {Path}", p);
        }
        #endregion
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (SceneEditor.Enabled)
        {
            if (SceneEditor.ShowExitConfirmationModal)
                return;
            SceneEditor.ShowExitConfirmationModal = true;
            e.Cancel = true;
        }
    }

    protected override void OnUnload()
    {
        base.OnUnload();

        PluginManager.UnloadPlugins();

        foreach (var lib in LoadedLibs)
        {
            Log.Debug("Unloading library {Library}", lib.Value);
            WinApi.FreeLibrary(lib.Key);
        }
    }
}