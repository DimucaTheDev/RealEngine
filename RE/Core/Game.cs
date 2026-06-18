using System.CommandLine;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using BulletSharp;
using BulletSharp.Math;
using Hexa.NET.ImGui;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using RE.Core;
using RE.Core.Assets;
using RE.Core.Assets.Providers;
using RE.Core.Audio;
using RE.Core.Initializing;
using RE.Core.Input;
using RE.Core.PluginSystem;
using RE.Core.Scripting;
using RE.Core.Ui;
using RE.Core.Ui.Debug;
using RE.Core.World;
using RE.Core.World.Components;
using RE.Core.World.Components.Physics;
using RE.Core.World.Physics;
using RE.Editor.Notification;
using RE.Editor.Panels.Viewport;
using RE.Launchers;
using RE.Rendering;
using RenderdocSharp;
using Serilog;
using Camera = RE.Rendering.Camera;
using Color = System.Drawing.Color;
using GL = OpenTK.Graphics.OpenGL.GL;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;
using SceneEditor = RE.Editor.SceneEditor;
using Vector3 = BulletSharp.Math.Vector3;

namespace RE.Utils;

//todo: refactor this class into smaller parts
internal partial class Game : GameWindow
{
    public static ParseResult CommandParseResult = null!;
    public const int FpsLock = 165; // fixme: fpsLock above 200 may (and will) cause physics issues
    private static bool _initialized, _closing;

    public static void Start(string[] args)
    {
        Thread.CurrentThread.Name = "Render Thread";
        Environment.CurrentDirectory = AppContext.BaseDirectory;

        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;
        var stdout = new StreamWriter(Console.OpenStandardOutput(), Encoding.Default) { AutoFlush = true };
        Console.SetOut(stdout);

        ParseArguments(args);
        SetupLogger();

        Log.Information("{ProductName}; version {Version}; build {BuildDate:dd.MM.yyyy HH:mm:ss}; commit {CommitHash}",
            ProductName, Version, BuildDate, CommitHash[..7]);
        Log.Information("Startup args: {@Args}", args);

        Directory.CreateDirectory("Engine/Config");
        if (Directory.Exists("Debug"))
        {
            Directory.Delete("Debug", true);
            Log.Debug("Deleted Debug Directory");
        }

        var nativesPath = CommandParseResult.GetValue<string>("--natives-path")!;
        nativesPath = Path.GetFullPath(nativesPath);

        foreach (var lib in Directory.GetFiles(nativesPath, "*.dll"))
        {
            nint handle;
            LoadedLibs.TryAdd(handle = WinApi.LoadLibrary(lib), lib);
            var fName = Path.GetFileName(lib);
            var errCode = Marshal.GetLastWin32Error();

            if (errCode == 0)
                Log.Debug("Loaded DLL {FileName,-15}: 0x{Handle,-16:x} Code: {ErrorCode}", fName, handle, errCode);
            else
                Log.Debug("Error loading DLL {FileName,-15}: {ErrorCode}, {ErrorMessage}", fName, errCode,
                    Marshal.GetLastPInvokeErrorMessage());
        }

        if (!HasMSVCRuntime(out var list))
        {
            WinApi.MessageBox(0,
                "Microsoft Visual C++ Runtime is required, but is not installed!\n" +
                "\n" +
                $"Could not load these libs: {string.Join(" ", list)}",
                "MSVC not found",
                16);
        }

        CommandHandler.RegisterCommandsInAssembly(Assembly.GetExecutingAssembly());
        
        PluginManager.ResolvePlugins();

        var width = CommandParseResult.GetValue<int>("--width");
        var height = CommandParseResult.GetValue<int>("--height");


        using var game = new Game(
            new GameWindowSettings { UpdateFrequency = FpsLock },
            new NativeWindowSettings
            {
                Title = $"{ProductName} {Version}",
                ClientSize = new Vector2i(width, height),
                Location = new Vector2i(Screen.PrimaryScreen!.Bounds.Width / 2 - width / 2,
                    Screen.PrimaryScreen.Bounds.Height / 2 - height / 2),
                StartVisible = false,
                Vsync = VSyncMode.Off,
                NumberOfSamples = CommandParseResult.GetValue<int>("--msaa")
            });

        GL.GetInteger(GetPName.Samples, out int samples);
        Log.Debug("MSAA set to {Samples} samples", samples);

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
                Log.Information("Joystick disconnected: {Name} ({JoystickId})", JoystickNames[e.JoystickId],
                    e.JoystickId);
                JoystickNames.Remove(e.JoystickId);
            }
        };

        if (Renderdoc.IsAvailable)
            Log.Information("RenderDoc available: {Version}", Renderdoc.Version);

        game.Run();
    }

    protected override void OnLoad()
    {
        if (EditorLauncher.Invoked)
            return;

        GL.Enable(EnableCap.DebugOutput);
        GL.Enable(EnableCap.DebugOutputSynchronous);

        GL.DebugMessageCallback(GlLogCallback, 0);
        GL.DebugMessageControl(DebugSourceControl.DontCare, DebugTypeControl.DontCare, DebugSeverityControl.DontCare, 0,
            (int[]?)null, false);
        GL.DebugMessageControl(DebugSourceControl.DontCare, DebugTypeControl.DebugTypeError,
            DebugSeverityControl.DebugSeverityHigh, 0, (int[]?)null, true);
        //GL.DebugMessageControl(DebugSourceControl.DontCare, DebugTypeControl.DontCare, DebugSeverityControl.DebugSeverityMedium, 0, (int[]?)null, true);

        ContentManager.Register(new FileContentProvider());
        ContentManager.Register(new ZipContentProvider());

        Icon = LoadIcon();

        RenderManager.Init();
        Camera.Init();
        ImGuiController.Init();
        DebugOverlay.Init();
        LineRenderer.Main!.StartRender();
        ConsoleWindow.Init();
        SoundManager.Init();
        PhysicsManager.Init();
        SceneEditor.Instance = new();
        CommandHandler.RegisterAllCommands();
        Initializer.AddStep(new InitializingTask
        {
            Label = "Doing our doings in default.cfg",
            Action = () => CommandHandler.ExecuteCommand("source assets/cfg/default.cfg")
        });

        IsVisible = true;
        WindowState = WindowState.Maximized;

        base.OnLoad();

        if (SceneManager.CurrentScene == null!)
            Log.Warning("No scene has been loaded. ({Name} = {Value})", nameof(SceneManager.CurrentScene), null);
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        Camera.Main.RenderWidth = SceneEditor.Enabled ? (int)ViewportPanel.ViewportSize.X : e.Width;
        Camera.Main.RenderHeight = SceneEditor.Enabled ? (int)ViewportPanel.ViewportSize.Y : e.Height;

        var w = Camera.Main.RenderWidth;
        var h = Camera.Main.RenderHeight;
        
        GL.Viewport(0, 0, w, h);
        
        ResizeFramebuffers(w, h);
        ResizeOitFramebuffer(w, h);
        
        base.OnResize(e);
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        FrameProfiler.BeginFrame();

        Time.Update(args);
        SoundManager.Update(args);

        if (_initialized && (!SceneEditor.Enabled || (SceneEditor.Enabled && SceneEditor.SimulationRunning)) &&
            SceneManager.CurrentScene != null!)
        {
            FrameProfiler.Begin("update");

            PhysicsManager.Update((float)args.Time);

            FrameProfiler.Begin("components");
            Keyboard.IsInputEnabled = !(SceneEditor.Enabled && SceneEditor.SimulationRunning);
            foreach (var scene in SceneManager.CurrentScene.GameObjects)
            {
                foreach (var c in scene.Components.Where(s => s.IsEnabled))
                {
                    c.Update(args);
                }
            }
            Keyboard.IsInputEnabled = true;

            SceneSerializer.ApplyObjectModification();
            FrameProfiler.End();

            FrameProfiler.Begin("ui");
            Hud.Update(args);
            FrameProfiler.End();

            FrameProfiler.End();
        }
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        if (_closing)
        {
            return;
        }

        var pb = FrameProfiler.Begin;
        var pe = FrameProfiler.End;

        pb("render");

        Context.MakeCurrent();

        if (!(_initialized = !Initializer.Render(args)))
        {
            return;
        }

        #region GL

        GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        GL.DepthFunc(DepthFunction.Lequal);
        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.Blend);
        GL.Enable(EnableCap.Multisample);
        GL.ClearColor(Color.CadetBlue);
        GL.PolygonMode(TriangleFace.FrontAndBack, Wireframe ? PolygonMode.Line : PolygonMode.Fill);

        #endregion

        pb("imgui_update");
        {
            ImGuiController.Update();
        }
        pe();

        if (ToastManager.MainWindowViewport == (ImGuiViewportPtr)null)
            ToastManager.MainWindowViewport = ImGui.GetMainViewport();

        if (SceneEditor.Enabled)
            SceneFramebuffer.Bind();

        var w = Camera.GetActiveCamera().RenderWidth;
        var h = Camera.GetActiveCamera().RenderHeight;
        GL.Viewport(0, 0, w, h);

        GL.ClearColor(Color.Black);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.Blend);
        GL.DepthFunc(DepthFunction.Lequal);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.PolygonMode(TriangleFace.FrontAndBack, Wireframe ? PolygonMode.Line : PolygonMode.Fill);

        RenderManager.RenderAll(args);
        SceneSerializer.ApplyObjectModification();

        // damn check how these brackets look!!! sad there are no macros :(
        pb("imgui_render");
        {
            if (SceneEditor.Enabled)
            {
                SceneFramebuffer.Unbind();
                GL.ClearColor(Color4.Black);
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
            }

            GL.Disable(EnableCap.Multisample);
            ToastManager.RenderNotifications();
            ImGuiController.Render();
            GL.Enable(EnableCap.Multisample);
        }
        pe();

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
            Instance.ToggleFullscreen();
        if (Keyboard.IsKeyPressed(Keys.F4, true))
            CommandHandler.ExecuteCommand("editor");
        if (Keyboard.IsKeyPressed(Keys.F5, true))
            CommandHandler.ExecuteCommand("debug"); 

        if (Keyboard.IsKeyPressed(Keys.F2, true))
        {
            var p = TakeScreenshot();
            if (p != null!)
                Log.Information("Screenshot saved to {Path}", p);
        }

        if (Keyboard.IsKeyPressed(Keys.KeyPadEnter))
        {
            for (int i = 0; i < 100; i++)
            {
                var a = new GameObject();
                a.Transform.Position = new OpenTK.Mathematics.Vector3(0, 10, 0);
                a.Tag = "prop";
                a.Components.Add(new MeshComponent("assets/models/cub.fbx"));
                a.Components.Add(new RigidBodyComponent(20));
                a.Components.Add(new BoxColliderComponent());
                SceneManager.CurrentScene.GameObjects.Add(a);
            }
        }

        #endregion

        FrameProfiler.End();
        FrameProfiler.EndFrame();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (SceneEditor.Enabled)
        {
            if (SceneEditor.ShowExitConfirmationModal)
                return;
            SceneEditor.ShowExitConfirmationModal = true;
            unsafe
            {
                WinApi.StartFlashing((IntPtr)WindowPtr);
            }

            e.Cancel = true;
        }
    }

    public override void Close()
    {
        _closing = true;
    } 

    protected override void OnUnload()
    {
        base.OnUnload();

        PluginManager.UnloadPlugins();
        SoundManager.Destroy();
        PhysicsManager.Destroy();
        Log.Information("End");
        Log.CloseAndFlush();
    }
}