#define THROW_ON_GL_EXCEPTION

using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.Desktop;
using RE.Core.Assets;
using RE.Core.Logging;
using RE.Core.Scripting;
using RE.Editor.Notification;
using RE.Rendering;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using FramebufferAttachment = OpenTK.Graphics.OpenGL.FramebufferAttachment;
using FramebufferErrorCode = OpenTK.Graphics.OpenGL.FramebufferErrorCode;
using FramebufferTarget = OpenTK.Graphics.OpenGL.FramebufferTarget;
using Image = OpenTK.Windowing.Common.Input.Image;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using PixelInternalFormat = OpenTK.Graphics.OpenGL.PixelInternalFormat;
using Rectangle = System.Drawing.Rectangle;
using RenderbufferStorage = OpenTK.Graphics.OpenGL.RenderbufferStorage;
using RenderbufferTarget = OpenTK.Graphics.OpenGL.RenderbufferTarget;
using TextureMagFilter = OpenTK.Graphics.OpenGL.TextureMagFilter;
using TextureParameterName = OpenTK.Graphics.OpenGL.TextureParameterName;
using TextureTarget = OpenTK.Graphics.OpenGL.TextureTarget;

#pragma warning disable IDE0130

namespace RE.Utils
{
    internal partial class Game
    {
        internal Game(GameWindowSettings gws, NativeWindowSettings nws) : base(gws, nws) { }

        public static Game Instance { get; internal set; } = null!;
        public static readonly DateTime BuildDate =
            Assembly.GetExecutingAssembly().GetCustomAttribute<BuildDateAttribute>()?.DateTime.ToLocalTime() ??
            DateTime.MinValue;
        public static readonly string CommitHash = Assembly.GetExecutingAssembly().GetCustomAttribute<GitCommitAttribute>()?.CommitHash ?? "unknown";
        public static readonly string Version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

        public const string ProductName = "Real Engine";

        internal static readonly Dictionary<nint, string> LoadedLibs = new();
        internal static readonly Dictionary<int, string> JoystickNames = [];

        private static bool Wireframe
        {
            get => (bool)(Variables.GetVariable("wireframe") ?? false);
            set => Variables.SetVariable("wireframe", value);
        }

        public int SceneFboId;
        public int SceneTextureId;
        public int SceneRboId;
        public int AccumColorTex;
        public int AccumWeightTex;
        public int OitFbo;
        public int OitDepthTexture;

        /// <summary>
        /// Takes a screenshot of the current frame and saves it to the <c>My Pictures</c> folder.
        /// </summary>
        /// <remarks>
        /// The screenshot will be saved in a subfolder named <see cref="ProductName"/> with a filename <c>re_yyyy-MM-dd_HH-mm-ss.png</c>.
        /// </remarks>
        /// <returns>Absolute path to saved screenshot</returns>
        public static string TakeScreenshot() => TakeScreenshot(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), ProductName,
            $"re_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png"));
        //todo: make methods non-static
        /// <summary>
        /// Takes a screenshot of the current frame and saves it to the specified file.
        /// </summary>
        /// <param name="fileName">Path that screenshot will be saved to</param>
        /// <returns>Absolute path to saved screenshot</returns>
        public static unsafe string TakeScreenshot(string fileName)
        {
            try
            {
                var a = new byte[Instance.ClientSize.X * Instance.ClientSize.Y * 3];
                fixed (byte* ptr = a)
                    GL.ReadPixels(0, 0, Instance.ClientSize.X, Instance.ClientSize.Y,
                        OpenTK.Graphics.OpenGL.PixelFormat.Rgb, PixelType.UnsignedByte, (IntPtr)ptr);
                Image<Rgb24> image =
                    SixLabors.ImageSharp.Image.LoadPixelData<Rgb24>(a, Instance.ClientSize.X, Instance.ClientSize.Y);
                image.Mutate(s => s.Flip(FlipMode.Vertical));

                Directory.CreateDirectory(Path.GetDirectoryName(fileName)!);

                image.SaveAsPng(fileName);
                image.Dispose();
                var full = Path.GetFullPath(fileName);
                ToastManager.InsertNotification(new Toast(ToastType.Success, "Screenshot saved.", 2));
                return full;
            }
            catch (Exception e)
            {
                Log.Error(e, "Unable to save screenshot to {FileName}", fileName);
                ToastManager.InsertNotification(new Toast(ToastType.Error, $"Unable to save screenshot:\n.{e}"));
            }

            return null!;
        }

        internal static void SetupLogger()
        {
            var fileInfo = CommandParseResult.GetValue<FileInfo?>("--log-template");

            var hasTemplate = fileInfo != null;
            var consoleTemplate = hasTemplate ? File.ReadAllText(fileInfo!.FullName) :
                "[{Timestamp:HH:mm:ss.fff} {Level:u3}] [{ThreadName}] [{SourceContext:Name}] {Message:lj}{NewLine}{Exception}";

            var minLevel = CommandParseResult.GetValue<LogEventLevel>("--log-level");
            Directory.CreateDirectory("Engine/Logs");
            File.Delete("Engine/Logs/Latest.log");

            var ansiConsoleTheme = new AnsiConsoleTheme(new Dictionary<ConsoleThemeStyle, string>
            {
                [ConsoleThemeStyle.Text] = "\u001B[38;5;15m",
                [ConsoleThemeStyle.SecondaryText] = "\u001B[38;5;7m",
                [ConsoleThemeStyle.TertiaryText] = "\u001B[38;5;8m",

                [ConsoleThemeStyle.Invalid] = "\u001B[38;5;11m",
                [ConsoleThemeStyle.Null] = "\u001B[38;5;12m",
                [ConsoleThemeStyle.Name] = "\u001B[38;5;7m",
                [ConsoleThemeStyle.String] = "\u001B[38;5;14m",
                [ConsoleThemeStyle.Number] = "\u001B[38;5;13m",
                [ConsoleThemeStyle.Boolean] = "\u001B[38;5;12m",
                [ConsoleThemeStyle.Scalar] = "\u001B[38;5;10m",

                [ConsoleThemeStyle.LevelVerbose] = "\u001B[38;5;7m",
                [ConsoleThemeStyle.LevelDebug] = "\u001B[38;5;7m",
                [ConsoleThemeStyle.LevelInformation] = "\u001B[38;5;15m",
                [ConsoleThemeStyle.LevelWarning] = "\u001B[38;5;11m",

                [ConsoleThemeStyle.LevelError] =
                    "\u001B[38;5;15m\u001B[48;5;9m",

                [ConsoleThemeStyle.LevelFatal] =
                    "\u001B[38;5;15m\u001B[48;5;9m"
            });

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(LogEventLevel.Verbose)
                .Enrich.WithThreadName()
                .Enrich.With(new EngineLoggerEnricher())
                .WriteTo.Sink(new GameLogger("[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"), minLevel)
                .WriteTo.FileEx(
                    path: "Engine/Logs/Latest.log",
                    outputTemplate: consoleTemplate, restrictedToMinimumLevel: LogEventLevel.Debug)
                .WriteTo.Console(outputTemplate: consoleTemplate, restrictedToMinimumLevel: minLevel,
                    theme: ansiConsoleTheme, applyThemeToRedirectedOutput: true)
                .CreateLogger();

            Log.Information("Hello, World!");
            if (hasTemplate)
                Log.Information("Using log template from: {LogTemplatePath}", Path.GetRelativePath(".", fileInfo!.FullName));

            InitializeExceptionHandling();
        }

        public static void InitializeExceptionHandling()
        {
            AppDomain.CurrentDomain.UnhandledException += static (s, e) =>
            {
                static string InternalCheck64Bit() => Environment.Is64BitProcess ? "64-bit" : "32-bit";

                var ex = (Exception)e.ExceptionObject;
                var process = Process.GetCurrentProcess();
                var memInfo = GC.GetGCMemoryInfo();

                StringBuilder report = new StringBuilder();
                report.AppendLine("--- FATAL ERROR REPORT ---");
                report.AppendLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                report.AppendLine($"OS: {RuntimeInformation.OSDescription} ({InternalCheck64Bit()})");
                report.AppendLine($"CPU: {Environment.ProcessorCount} cores, {RuntimeInformation.ProcessArchitecture}");
                report.AppendLine($"Runtime: {RuntimeInformation.FrameworkDescription}");
                report.AppendLine($"Process ID: {process.Id}");
                report.AppendLine($"Thread: [{Thread.CurrentThread.ManagedThreadId}] {Thread.CurrentThread.Name}");
                report.AppendLine($"Memory: WorkingSet {process.WorkingSet64 / 1024 / 1024}MB, GC Heap {GC.GetTotalMemory(false) / 1024 / 1024}MB");
                report.AppendLine($"GC Strategy: {memInfo.Index} (HighMemoryLoad Threshold: {memInfo.HighMemoryLoadThresholdBytes / 1024 / 1024}MB)");

                if (false)
                {
                    report.AppendLine("--- LOADED MODULES ---");
                    foreach (ProcessModule module in process.Modules)
                    {
                        try
                        {
                            report.AppendLine(
                                $"0x{module.BaseAddress:x}\t-\t{module.ModuleMemorySize / 1024} Kb \t{module.ModuleName}");
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                }

                Log.Fatal(ex, "{Report}\nException Detail:", report.ToString());
                Log.CloseAndFlush();
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                Log.Error(e.Exception, "Unobserved Task Exception (Async Leak)");
                e.SetObserved();
            };

            Application.ThreadException += (s, e) =>
                Log.Fatal(e.Exception, "UI/WinForms Thread Exception");

            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                Log.Information("Engine shutdown initiated (ProcessExit)");
                Log.CloseAndFlush();
            };
        }

        [StackTraceHidden]
        internal static void GlLogCallback(DebugSource source, DebugType type, int id, DebugSeverity severity, int length, IntPtr message, IntPtr userParam)
        {
            if (type == DebugType.DebugTypeOther)
                return;

            string msg = Marshal.PtrToStringAnsi(message, length);
            Log.Write(severity switch
            {
                DebugSeverity.DontCare => LogEventLevel.Verbose,
                DebugSeverity.DebugSeverityHigh => LogEventLevel.Error,
                DebugSeverity.DebugSeverityMedium => LogEventLevel.Warning,
                DebugSeverity.DebugSeverityLow => LogEventLevel.Information,
                DebugSeverity.DebugSeverityNotification => LogEventLevel.Verbose,
                _ => LogEventLevel.Information
            }, "[{OpenGL}:{Action}] {Message}", "OpenGL", type, msg);
#if THROW_ON_GL_EXCEPTION
            if (severity == DebugSeverity.DebugSeverityHigh)
                throw new GlException(msg);
#endif
        }

        internal static WindowIcon? LoadIcon()
        {
            var path = "Assets/RealEngine.ico";
            if (!ContentManager.Exists(path))
            {
                Log.Error("Icon file not found: {IconPath}", path);
                return null;
            }
            try
            {
                using var icon = new Icon(ContentManager.Open(path));
                using var bitmap = icon.ToBitmap();

                var data = new byte[bitmap.Width * bitmap.Height * 4];
                var bitmapData = bitmap.LockBits(
                    new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    ImageLockMode.ReadOnly,
                    PixelFormat.Format32bppArgb);

                Marshal.Copy(bitmapData.Scan0, data, 0, data.Length);
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

        /// <summary>
        /// Toggles window state between fullscreen and windowed mode.
        /// </summary>
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

        public void SetupOitFbo(int width, int height)
        {
            GL.GenFramebuffers(1, out OitFbo);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, OitFbo);

            GL.GenTextures(1, out AccumColorTex);
            GL.BindTexture(TextureTarget.Texture2D, AccumColorTex);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f,
                width, height, 0, OpenTK.Graphics.OpenGL.PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, AccumColorTex, 0);

            GL.GenTextures(1, out AccumWeightTex);
            GL.BindTexture(TextureTarget.Texture2D, AccumWeightTex);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R16f,
                width, height, 0, OpenTK.Graphics.OpenGL.PixelFormat.Red, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, TextureTarget.Texture2D, AccumWeightTex, 0);

            GL.GenTextures(1, out OitDepthTexture);
            GL.BindTexture(TextureTarget.Texture2D, OitDepthTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent24,
                width, height, 0, OpenTK.Graphics.OpenGL.PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, OitDepthTexture, 0);

            DrawBuffersEnum[] buffers = [DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1];
            GL.DrawBuffers(2, buffers);

            var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != FramebufferErrorCode.FramebufferComplete)
                throw new GlException($"OIT FBO incomplete: {status}");

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }
        public void SetupSceneFbo(int width, int height)
        {
            if (SceneFboId != 0)
            {
                GL.DeleteFramebuffer(SceneFboId);
                GL.DeleteTexture(SceneTextureId);
                GL.DeleteRenderbuffer(SceneRboId);
            }

            GL.GenFramebuffers(1, out SceneFboId);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, SceneFboId);

            GL.GenTextures(1, out SceneTextureId);
            GL.BindTexture(TextureTarget.Texture2D, SceneTextureId);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, width, height, 0, OpenTK.Graphics.OpenGL.PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, SceneTextureId, 0);

            GL.GenRenderbuffers(1, out SceneRboId);
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, SceneRboId);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.Depth24Stencil8, width, height);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, SceneRboId);

            if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
            {
                Log.Error("{Method}: GL Framebuffer is not complete", nameof(SetupSceneFbo));
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        internal static void ParseArguments(string[] args)
        {
            Option<int> widthOption = new("--width", "-w")
            {
                Description = "Width of game window in pixels.",
                DefaultValueFactory = _ => 1280
            };
            Option<int> heightOption = new("--height", "-h")
            {
                Description = "Height of game window in pixels.",
                DefaultValueFactory = _ => 720
            };
            Option<FileInfo?> logTemplateFileOption = new("--log-template", "-log")
            {
                Description = "Path to log template file.",
                CustomParser = res =>
                {
                    if (!res.Tokens.Any())
                        return null;
                    if (!File.Exists(res.Tokens.Single().Value))
                    {
                        res.AddError("Specified logger template does not exist.");
                        return null;
                    }
                    return new(res.Tokens.Single().Value);
                }
            };
            Option<bool> useSequentialTaskSchedulerOption = new("--phys-sequential", "-s")
            {
                Description = "[libbulletc] Sequential: Executes all tasks in a strict single-threaded order. No parallelism is used.",
                DefaultValueFactory = _ => true
            };
            Option<bool> useMpTaskSchedulerOption = new("--phys-mp", "-mp")
            {
                Description = "[libbulletc] Multi-Processing: Uses the OpenMP framework to parallelize workload across available CPU cores, primarily through directives on loops and code blocks."
            };
            Option<bool> useTbbTaskSchedulerOption = new("--phys-tbb", "-tbb")
            {
                Description = "[libbulletc] Threading Building Block: Employs the Intel TBB library and a work-stealing algorithm for efficient, high-level task parallelism and dynamic load balancing."
            };
            Option<bool> usePplTaskSchedulerOption = new("--phys-ppl", "-ppl")
            {
                Description = "[libbulletc] Parallel Patterns Library: Utilizes the Microsoft PPL for scalable task-based parallelism and streamlined flow control."
            };
            Option<string> nativesPathOption = new("--natives-path", "-n")
            {
                Description = "Location where Win32 native libraries are stored.",
                DefaultValueFactory = _ => "Engine/Natives",
                CustomParser = res =>
                {
                    if (!res.Tokens.Any())
                        return null;
                    var first = res.Tokens.Single();
                    if (!Directory.Exists(first.Value))
                    {
                        res.AddError($"Directory '{first.Value}' does not exist.");
                        return null;
                    }
                    return new(first.Value);
                }
            };
            Option<LogEventLevel> logLevelOption = new("--log-level", "-l")
            {
                Description = "Minimum logging level severity.",
                DefaultValueFactory = _ => Debugger.IsAttached ? LogEventLevel.Debug : LogEventLevel.Information,
                CustomParser = res =>
                {
                    var name = res.Tokens.Single().Value;

                    LogEventLevel level;

                    if (int.TryParse(name, out var i) && Enum.IsDefined(typeof(LogEventLevel), i))
                    {
                        level = (LogEventLevel)i;
                    }
                    else if (Enum.IsDefined(typeof(LogEventLevel), name))
                    {
                        level = Enum.Parse<LogEventLevel>(name, true);
                    }
                    else
                    {
                        level = LogEventLevel.Information;
                        res.AddError($"Unknown log level '{name}'. Possible values are: {string.Join(", ", Enum.GetNames(typeof(LogEventLevel)))}.");
                    }

                    return level;
                }
            };
            Option<bool> attachDebugger = new Option<bool>("--debugger", "-d")
            {
                Action = new AttachDebuggerAction()
            };
            Option<long> consoleOption = new Option<long>("--console", "-c")
            {
                Description = "Allocate a new console OR set output stream to specified handle.",
                Arity = ArgumentArity.ZeroOrOne,
                CustomParser = p =>
                {
                    if (!p.Tokens.Any())
                    {
                        if (WinApi.AllocConsole())
                        {
                            SetupConsolePipes(IntPtr.Zero);
                        }
                        return 0;
                    }
                    var s = p.Tokens.Single().Value;
                    long.TryParse(s, out long value);

                    IntPtr handle = (IntPtr)value;
                    SetupConsolePipes(handle);

                    return value;
                }
            };
            Option<string?> editorOption = new Option<string?>("--editor", "-e")
            {
                Description = "[Editor Launcher] Load scene in Scene Editor.",
                DefaultValueFactory = _ => null
            };

            RootCommand rootCommand = new($"{ProductName} command line options")
            {
                widthOption,
                heightOption,
                logTemplateFileOption,
                useSequentialTaskSchedulerOption,
                useMpTaskSchedulerOption,
                useTbbTaskSchedulerOption,
                usePplTaskSchedulerOption,
                nativesPathOption,
                logLevelOption,
                attachDebugger,
                consoleOption,
                editorOption
            };
            rootCommand.TreatUnmatchedTokensAsErrors = true;

            var result = rootCommand.Parse(args);
            result.Invoke();

            if (result.Errors.Any() || result.Action?.GetType() == typeof(HelpAction))
            {
                Thread.Sleep(5000);
                Environment.Exit(0);
                return;
            }
            CommandParseResult = result;
        }

        static void SetupConsolePipes(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                var stdOut = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
                Console.SetOut(stdOut);
            }
            else
            {
                Console.WriteLine(handle);
                var safeHandle = new SafeFileHandle(handle, false);
                var writer = new StreamWriter(new FileStream(safeHandle, FileAccess.Write)) { AutoFlush = true };
                Console.SetOut(writer);
                Console.SetError(writer);
            }
        }

        private class AttachDebuggerAction : SynchronousCommandLineAction
        {
            public override int Invoke(ParseResult parseResult)
            {
                if (!Debugger.Launch() || !Debugger.IsAttached)
                {
                    Console.WriteLine("Debugger is not connected.");
                    Environment.Exit(-1);
                    return -1;
                }
                return 0;
            }
        }
    }
}
