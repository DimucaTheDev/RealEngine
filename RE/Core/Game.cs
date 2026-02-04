using System.CommandLine;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Hexa.NET.ImGui;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using RE.Audio;
using RE.Core.Assets;
using RE.Core.Assets.Providers;
using RE.Core.Input;
using RE.Core.PluginSystem;
using RE.Core.Scripting;
using RE.Core.World;
using RE.Core.World.Physics;
using RE.Debug;
using RE.Debug.Overlay;
using RE.Editor.Notification;
using RE.Editor.Panels.Viewport;
using RE.Launchers;
using RE.Rendering;
using RE.Utils;
using RenderdocSharp;
using Serilog;
using Camera = RE.Rendering.Camera;
using Color = System.Drawing.Color;
using GL = OpenTK.Graphics.OpenGL.GL;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;
using SceneEditor = RE.Editor.SceneEditor;
using Vector4 = System.Numerics.Vector4;

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

        Log.Information("{ProductName}; version {Version}; build {BuildDate:dd.MM.yyyy HH:mm:ss}; commit {CommitHash}", ProductName, Version, BuildDate, CommitHash[..7]);
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
                Title = $"{ProductName} {Version}",
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

    protected override unsafe void OnLoad()
    {
        if (EditorLauncher.Invoked)
            return;

        GL.Enable(EnableCap.DebugOutput);
        GL.Enable(EnableCap.DebugOutputSynchronous);

        GL.DebugMessageCallback(GlLogCallback, 0);
        GL.DebugMessageControl(DebugSourceControl.DontCare, DebugTypeControl.DontCare, DebugSeverityControl.DontCare, 0, (int[]?)null, false);
        GL.DebugMessageControl(DebugSourceControl.DontCare, DebugTypeControl.DebugTypeError, DebugSeverityControl.DebugSeverityHigh, 0, (int[]?)null, true);
        //GL.DebugMessageControl(DebugSourceControl.DontCare, DebugTypeControl.DontCare, DebugSeverityControl.DebugSeverityMedium, 0, (int[]?)null, true);

        ContentManager.Register(new FileContentProvider());
        ContentManager.Register(new ZipContentProvider());

        Icon = LoadIcon();

        RenderManager.Init();
        Time.Init();
        Camera.Init();
        ImGuiController.Init();
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
        Initializer.AddStep(("Running default.cfg", () =>
        {
            CommandHandler.ExecuteCommand("source assets/cfg/default.cfg");
            CommandHandler.ExecuteCommand("level lobby");
        }
        ));

        base.OnLoad();
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        RenderProfiler.StopAll();

        Time.Update(args);
        PhysicsManager.Update((float)args.Time);
        SoundManager.Update(args);

        var rp = RenderProfiler.StartNew("update");
        if (!SceneEditor.Enabled && SceneManager.CurrentScene != null!)
        {
            foreach (var scene in SceneManager.CurrentScene.GameObjects.ToList())
            {
                foreach (var c in scene.Components.ToList())
                {
                    c.Update(args);
                }
            }
        }
        rp.Stop();
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
        RenderProfiler.StartNew("render");

        Context.MakeCurrent();

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

        ImGuiController.Update();
        if (ToastManager.MainWindowViewport == (ImGuiViewportPtr)null)
            ToastManager.MainWindowViewport = ImGui.GetWindowViewport();


        if (SceneEditor.Enabled)
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, SceneFboId);

        var w = Camera.GetActiveCamera().RenderWidth;
        var h = Camera.GetActiveCamera().RenderHeight;
        GL.Viewport(0, 0, w, h);

        GL.ClearColor(Color.Black);
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

        ToastManager.RenderNotifications();
        ImGuiController.Render();
        SwapBuffers();

        #region Keybinds

        if (Keyboard.IsKeyPressed(Keys.F7, true))
        {
            var imGuiToast1 = new Toast(ToastType.None, "Hello World!");
            var imGuiToast125 = new Toast(ToastType.None);
            var imGuiToast15 = new Toast(ToastType.None, "Hello World!", "title");
            var imGuiToast2 = new Toast(ToastType.Info, "Hello World!");
            var imGuiToast25 = new Toast(ToastType.Info, "Hello World!", "title");
            var imGuiToast3 = new Toast(ToastType.Success, "Hello World!");
            var imGuiToast35 = new Toast(ToastType.Success);
            var imGuiToast4 = new Toast(ToastType.Warning, "Hello Woooooooooorld!");
            var imGuiToast45 = new Toast(ToastType.Warning, "World!", "Helloooooooooo");
            var imGuiToast5 = new Toast(ToastType.Error, "Hello World!");
            Time.Schedule(250 * 0, () => { ToastManager.InsertNotification(imGuiToast1); });
            Time.Schedule(250 * 0, () => { ToastManager.InsertNotification(imGuiToast125); });
            Time.Schedule(250 * 0, () => { ToastManager.InsertNotification(imGuiToast15); });
            Time.Schedule(250 * 1, () => { ToastManager.InsertNotification(imGuiToast2); });
            Time.Schedule(250 * 1, () => { ToastManager.InsertNotification(imGuiToast25); });
            Time.Schedule(250 * 2, () => { ToastManager.InsertNotification(imGuiToast3); });
            Time.Schedule(250 * 2, () => { ToastManager.InsertNotification(imGuiToast35); });
            Time.Schedule(250 * 3, () => { ToastManager.InsertNotification(imGuiToast4); });
            Time.Schedule(250 * 3, () => { ToastManager.InsertNotification(imGuiToast45); });
            Time.Schedule(250 * 4, () => { ToastManager.InsertNotification(imGuiToast5); });
        }

        if (Keyboard.IsKeyPressed(Keys.GraveAccent))
            ConsoleWindow.Instance!.IsVisible = !ConsoleWindow.Instance.IsVisible;
        if (Keyboard.IsKeyPressed(Keys.F3, true))
            Wireframe = !Wireframe;
        if (Keyboard.IsKeyPressed(Keys.F11, true))
            Game.Instance.ToggleFullscreen();
        if (Keyboard.IsKeyPressed(Keys.F4, true))
            CommandHandler.ExecuteCommand("editor");
        if (Keyboard.IsKeyPressed(Keys.F5, true))
            CommandHandler.ExecuteCommand("debug");
        if (Keyboard.IsKeyPressed(Keys.F1, true))
        {
            RenderManager.RemoveCameraFrustum();
            if (Keyboard.IsKeyDown(Keys.LeftControl))
            {
                return;
            }
            RenderManager.CreateCameraFrustum();
        }
        if (Keyboard.IsKeyPressed(Keys.F2, true))
        {
            var p = Game.TakeScreenshot();
            if (p != null!)
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
        SoundManager.Destroy();
        PhysicsManager.Destroy();
        ImGuiController.Destroy();
        Log.CloseAndFlush();
    }
}