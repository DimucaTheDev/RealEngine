using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using Hexa.NET.ImGui.Backends.Vulkan;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using RE.Audio;
using RE.Core;
using RE.Core.Assets;
using RE.Core.Assets.Providers;
using RE.Core.PluginSystem;
using RE.Core.Scripting;
using RE.Core.World;
using RE.Core.World.Physics;
using RE.Debug;
using RE.Debug.Overlay;
using RE.Editor;
using RE.Rendering;
using RE.Utils;
using RenderdocSharp;
using Serilog;

namespace RE.Launchers
{
    public class EditorLauncher
    {
        public static bool Invoked { get; private set; }
        public static void Run(string[] args)
        {
            throw new NotImplementedException();
            Invoked = true;

            Thread.CurrentThread.Name = "Render Thread";
            Environment.CurrentDirectory = AppContext.BaseDirectory;

            Game.ParseArguments(args);
            Game.SetupLogger();

            Log.Information(
                "{ProductName}; version {Version}; build {BuildDate:dd.MM.yyyy HH:mm:ss}; commit {CommitHash}",
                Game.ProductName, Game.Version, Game.BuildDate, Game.CommitHash[..7]);
            Log.Information("Startup args: {@Args}", args);

            if (Directory.Exists("Debug"))
                Directory.Delete("Debug", true);

            var nativesPath = Game.CommandParseResult.GetValue<string>("--natives-path")!;

            foreach (var lib in Directory.GetFiles(nativesPath, "*.dll"))
            {
                nint handle;
                Game.LoadedLibs.TryAdd(handle = WinApi.LoadLibrary(lib), lib);
                var fName = Path.GetFileName(lib);
                var errCode = Marshal.GetLastWin32Error();

                if (errCode == 0)
                    Log.Debug("Loaded DLL {FileName,-15}: 0x{Handle,-16:x} Code: {ErrorCode}", fName, handle, errCode);
                else
                    Log.Debug("Error loading DLL {FileName,-15}: {ErrorCode}, {ErrorMessage}", fName, errCode, Marshal.GetLastPInvokeErrorMessage());
            }

            PluginManager.ResolvePlugins();

            ContentManager.Register(new FileContentProvider());
            ContentManager.Register(new ZipContentProvider());
            var width = Game.CommandParseResult.GetValue<int>("--width");
            var height = Game.CommandParseResult.GetValue<int>("--height");
            using var game = new Game(
                new GameWindowSettings { UpdateFrequency = Game.FpsLock },
                new NativeWindowSettings
                {
                    Title = $"{Game.ProductName} {Game.Version}",
                    ClientSize = new Vector2i(width, height),
                    Location = new Vector2i(Screen.PrimaryScreen!.Bounds.Width / 2 - width / 2, Screen.PrimaryScreen.Bounds.Height / 2 - height / 2),
                    WindowState = WindowState.Normal
                });
            Game.Instance = game;
            Log.Information("Bootstrapping...");

            Time.Init();
            Camera.Init();
            RenderManager.Init();
            DebugOverlay.Init();
            LineRenderer.Main!.StartRender();
            ConsoleWindow.Init();
            SoundManager.Init();
            Log.Information("Sound Manager initialized.");
            PhysicsManager.Init();
            Log.Information("Bullet Physics initialized.");
            SceneEditor.Instance = new();
            Log.Information("Scene Editor initialized.");


            Log.Information("Registering commands...");
            CommandHandler.RegisterAllCommands();

            //Initializer.AddStep(("Running default.cfg", () => CommandHandler.ExecuteCommand("source assets/cfg/default.cfg")));

            var level = Game.CommandParseResult.GetValue<string?>("--editor");
            if (string.IsNullOrEmpty(level))
            {
                Console.Write("Enter map name: ");
                level = Console.ReadLine() ?? throw new Exception("???");
            }

            SceneManager.LoadScene(SceneManager.ParseScene(level!), true, SceneEditor.Instance.Enable);
            
            Game.Instance.Icon = Game.LoadIcon();

            RenderManager.Init();
            Camera.Init();
            ImGuiController.Init();
            Initializer.Init();

            GL.Enable(EnableCap.DebugOutput);
            GL.Enable(EnableCap.DebugOutputSynchronous);
            GL.DebugMessageCallback(Game.GlLogCallback, 0);

            Game.Instance.JoystickConnected += static e =>
            {
                if (e.IsConnected)
                {
                    Log.Information("Joystick connected: {Name} ({JoystickId})", Game.Instance.JoystickStates[e.JoystickId].Name,
                        e.JoystickId);
                    Game.JoystickNames[e.JoystickId] = Game.Instance.JoystickStates[e.JoystickId].Name;
                }
                else
                {
                    Log.Information("Joystick disconnected: {Name} ({JoystickId})", Game.JoystickNames[e.JoystickId], e.JoystickId);
                    Game.JoystickNames.Remove(e.JoystickId);
                }
            };

            if (Renderdoc.IsAvailable)
                Log.Information("RenderDoc available: {Version}", Renderdoc.Version);

            game.Run();

            Log.Information("End");
        }
    }
}
