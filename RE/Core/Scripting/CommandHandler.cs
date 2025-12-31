using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using OpenTK.Graphics.OpenGL;
using RE.Audio;
using RE.Core.Assets;
using RE.Core.World;
using RE.Debug.Overlay;
using RE.Rendering;
using RE.Utils;
using Serilog;
using SixLabors.ImageSharp;
using SceneEditor = RE.Editor.SceneEditor;

namespace RE.Core.Scripting
{
    /// <summary>
    /// Provides static methods and events for registering, executing, and managing text-based commands within the
    /// application.
    /// </summary>
    /// <remarks>The CommandHandler class enables dynamic registration of command handlers and supports
    /// command execution with argument parsing. It is designed for use in scenarios such as developer consoles or
    /// scripting interfaces, where commands are entered as strings and dispatched to registered handlers. All members
    /// are static, and the class is not intended to be instantiated. Thread safety is not guaranteed; ensure
    /// appropriate synchronization if accessing from multiple threads.</remarks>
    public static class CommandHandler
    {
        //mb i should make a Command class with description, args, etc

        public static event Action<string, List<string>, string?>? CommandExecuted;

        private static int _recursionDepth = 0;
        private static readonly Dictionary<string, string> CommandDescriptions = [];
        private const int MaxRecursionDepth = 100;

        /// <summary>
        /// Executes the specified command line string, ensuring that the maximum allowed recursion depth is not
        /// exceeded.
        /// </summary>
        /// <remarks>If the maximum recursion depth is exceeded, the command is not executed and an error
        /// is logged. This method is intended to prevent stack overflows or infinite recursion when executing nested
        /// commands.</remarks>
        /// <param name="line">The command line to execute. Cannot be null.</param>
        public static void ExecuteCommandSafe(string line)
        {
            if (_recursionDepth > MaxRecursionDepth)
            {
                Log.Error("Max recursion depth exceeded.");
                return;
            }
            try
            {
                _recursionDepth++;
                ExecuteCommand(line);
            }
            finally
            {
                _recursionDepth--;
            }
        }
        /// <summary>
        /// Parses and executes the specified command string, invoking the associated command handler if applicable.
        /// </summary>
        /// <remarks>The command string is split into arguments, supporting quoted substrings as single
        /// arguments. If a command handler is registered, it is invoked with the parsed command and arguments. Lines
        /// starting with '#' are treated as comments and ignored.</remarks>
        /// <param name="command">The command line to execute. Leading whitespace is ignored. If the command is null, empty, consists only of
        /// whitespace, or starts with a '#', the method does nothing.</param>
        public static void ExecuteCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command) || command.TrimStart(' ').StartsWith("#"))
                return;

            var matches = Regex.Matches(command, @"[\""].+?[\""]|\S+");
            var result = new string[matches.Count];
            for (int i = 0; i < matches.Count; i++)
            {
                string value = matches[i].Value;
                if (value.StartsWith("\"") && value.EndsWith("\""))
                    value = value.Substring(1, value.Length - 2);
                result[i] = value;
            }
            try
            {
                CommandExecuted?.Invoke(result[0], result[1..].ToList(), command);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error executing command \"{Command} {Args}\"", command, string.Join(' ', result[1..].ToList()));
            }
        }
        /// <summary>
        /// Registers a command handler that is invoked when a command with the specified name is executed.
        /// </summary>
        /// <remarks>If a handler is already registered for the specified command name, this method does
        /// not overwrite the existing handler and logs a warning instead. Each command name can only have one
        /// associated handler.</remarks>
        /// <param name="name">The name of the command to associate with the handler. Command names are compared using case-insensitive
        /// ordinal comparison. Cannot be null or empty.</param>
        /// <param name="handler">The action to execute when the command is triggered. Receives the list of command arguments as its
        /// parameter. Cannot be null.</param>
        /// <param name="description">An optional description of the command. If not specified, the description is set to an empty string.</param>
        public static void RegisterHandler(string name, Action<List<string>> handler, string description = "")
        {
            if (!CommandDescriptions.TryAdd(name, description))
            {
                Log.Warning("Command {Command} is already registered!", name);
                return;
            }

            CommandExecuted += (cmd, args, _) =>
            {
                if (cmd.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    handler(args);
                }
            };
        }
        /// <summary>
        /// Registers a command handler that is invoked when a command with a single string argument is executed.
        /// </summary>
        /// <remarks>If a command with the specified name is already registered, this method does not
        /// overwrite the existing handler and logs a warning instead. Only one handler can be registered per command
        /// name.</remarks>
        /// <param name="name">The name of the command to register. Command names are compared using case-insensitive ordinal comparison.</param>
        /// <param name="handler">The action to execute when the command is invoked. The handler receives the full argument string passed to
        /// the command.</param>
        /// <param name="description">An optional description of the command. The description can be used for help text or documentation purposes.
        /// The default is an empty string.</param>
        public static void RegisterSingleArgHandler(string name, Action<string> handler, string description = "")
        {
            if (!CommandDescriptions.TryAdd(name, description))
            {
                Log.Warning("Command {Command} is already registered!", name);
                return;
            }
            CommandExecuted += (cmd, args, full) =>
            {
                if (cmd.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    handler(full!);
                }
            };
        }

        internal static void RegisterAllCommands()
        {
            RegisterHandler("help", _ =>
            {
                Log.Information("Available commands:");
                foreach (var command in CommandDescriptions)
                {
                    Log.Information(" - {Command}: {Description}", command.Key, command.Value);
                }
            }, "Get all commands");
            RegisterHandler("var", list =>
            {
                //todo: list support for args > 2
                if (list.Count == 2)
                {
                    string key = list[0];
                    object value = list[1];
                    if (float.TryParse(list[1].Replace("f", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out float floatValue))
                        value = floatValue;
                    else if (bool.TryParse(list[1], out bool boolValue))
                        value = boolValue;
                    else if (list[1].Equals("null", StringComparison.OrdinalIgnoreCase))
                        value = null!;
                    Variables.SetVariable(key, value);
                    Log.Information("'{Key}' set to {Value}", key, Format(value));
                }
                else
                {
                    var value = Variables.GetVariable(list[0]);

                    //todo: return variable value, make Func<> instead of Action<>
                    string content = value != null ? $"{list[0]}:{value.GetType().Name} = " : $"{list[0]} = ";

                    content += Format(value);

                    Log.Information(content);
                }
            }, "Set a value to the variable");
            RegisterHandler("vars", _ =>
            {
                int l = Math.Max(15, Variables.GlobalVariables.Keys.Select(s => s.Length).Max());
                int valueWidth = 15;
                int totalWidth = l + valueWidth + 1;
                string horizontalLine = new string('-', totalWidth);

                Log.Information(horizontalLine);
                Log.Information($"{"NAME".PadRight(l)}|{"VALUE".PadLeft(valueWidth)}");
                Log.Information(new string('-', l) + "+" + new string('-', valueWidth));

                foreach (var variable in Variables.GlobalVariables)
                {
                    Log.Information("{Name}|{Value}", variable.Key.PadRight(l), Format(variable.Value).PadLeft(valueWidth));
                }
                Log.Information(horizontalLine);

            }, "Get all variables in formatted table");
            RegisterHandler("clear", _ =>
            {
                GameLogger.Log = "";
            }, "Clear the log");
            RegisterHandler("sound", list =>
            {
                if (list.Count == 0)
                {
                    Log.Error("Usage: {Usage}", "sound play <name> [volume] [inWorld] [maxDistance] [referenceDistance]");
                    Log.Error("Usage: {Usage}", "sound stopall");
                    return;
                }

                if (list[0] == "stopall")
                    SoundManager.StopAll();
                if (list[0] == "play") // sound play <name> [volume] [inWorld] [maxDistance] [referenceDistance]
                {
                    if (list.Count < 2)
                    {
                        Log.Error("Usage: {Usage}", "sound play <name> [volume] [inWorld] [maxDistance] [referenceDistance]");
                        return;
                    }
                    string name = list[1];
                    float volume = list.Count > 2 ? float.Parse(list[2]) : 1f;
                    bool inWorld = list.Count > 3 && bool.Parse(list[3]);
                    float maxDistance = list.Count > 4 ? float.Parse(list[4]) : 10f;
                    float referenceDistance = list.Count > 5 ? float.Parse(list[5]) : 1f;
                    SoundManager.Play(name, new()
                    {
                        Volume = volume,
                        InWorld = inWorld,
                        MaxDistance = maxDistance,
                        ReferenceDistance = referenceDistance
                    });
                }
            }, "Play or stop sound. Enter command with no arguments to see more info");
            RegisterHandler("exit", _ => Game.Instance.Close(), "Close the game");
            RegisterHandler("source", list =>
            {
                if (list.Count == 0)
                {
                    Log.Error("No script specified!");
                    return;
                }

                if (!ContentManager.Exists(list[0]))
                {
                    Log.Error("File not found: {FilePath}", list[0]);
                    return;
                }
                string src = ContentManager.GetString(list[0]);
                foreach (var line in src.Split('\n'))
                {
                    ExecuteCommandSafe(line);
                }
            }, "Run script in selected path");
            RegisterSingleArgHandler("echo", c =>
            {
                Log.Information(new string(c[c.IndexOf(' ')..].SkipWhile(s => s == ' ').ToArray()));
            }, "Prints to log with Info level");
            RegisterHandler("frustum", args =>
            {
                if (!args.Any())
                {
                    Log.Error("Usage: {Usage}", "frustum create|destroy");
                    return;
                }

                switch (args.First())
                {
                    case "create":
                        RenderManager.CreateCameraFrustum();
                        break;
                    case "destroy":
                        RenderManager.RemoveCameraFrustum();
                        break;
                    default:
                        Log.Error("Usage: {Usage}", "frustum create|destroy");
                        break;
                }
            }, "Renders or removes camera frustum");
            RegisterHandler("level", args =>
            {
                if (args.Count == 0)
                    Log.Information("Current level: {Level}", SceneManager.CurrentScene.Name ?? "<unnamed>");
                else
                {
                    SceneEditor.Instance?.Disable();
                    var name = args[0];
                    if (!ContentManager.Exists($"assets/maps/{name}/data.json"))
                    {
                        Log.Error("File not found: {FilePath}", $"assets/maps/{name}/data.json");
                        return;
                    }
                    Log.Information("Loading {Level}... ", name);
                    SceneManager.LoadScene(SceneManager.ParseScene(name), true, () =>
                    {
                        SoundManager.Play("end_flash", new SoundPlaybackSettings() { DisposeOnStop = true, InWorld = false });
                    });
                }
            }, "Print level name or loads new one");
            RegisterHandler("editor", args =>
            {
                var ov = SceneEditor.Instance;
                if (!SceneEditor.Enabled)
                    ov.Enable();
                else
                    Log.Error("Editor can be closed only via Editor -> Exit");
                //   ov.Disable();
            }, "Starts the scene editor for current level");
            RegisterHandler("credits", _ =>
            {
                WinApi.MessageBox(0, "Made with ❤️ by DimucaTheDev", "About", 0x40);
            });
            RegisterHandler("dt", _ =>
            {
                Directory.CreateDirectory("dump");
                Directory.EnumerateFiles("dump").ToList().ForEach(File.Delete);
                Log.Information("Dumping textures...");
                int index = 0;
                foreach (var tex in Enumerable.Range(0, 32000)) // https://www.youtube.com/shorts/gib8WGoR604 
                {
                    if (!GL.IsTexture(tex))
                        continue;
                    GL.BindTexture(TextureTarget.Texture2D, tex);

                    GL.GetTexLevelParameter(TextureTarget.Texture2D, 0, GetTextureParameter.TextureWidth, out int width);
                    GL.GetTexLevelParameter(TextureTarget.Texture2D, 0, GetTextureParameter.TextureHeight, out int height);
                    if (width == 0 || height == 0)
                        continue;
                    byte[] pixels = new byte[width * height * 4];
                    GL.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);


                    using var img = SixLabors.ImageSharp.Image.LoadPixelData<SixLabors.ImageSharp.PixelFormats.Rgba32>(pixels, width, height);
                    img.SaveAsPng($"dump/{tex}.png");
                    index++;
                }
                Log.Information("Dumped {Count} textures.", index);
            }, "Dumps all GL textures to dump/ folder");
            RegisterSingleArgHandler("debug", s =>
            {
                DebugOverlay.Instance.IsVisible = !DebugOverlay.Instance.IsVisible;
            }, "Open or close debug overlay");
            RegisterSingleArgHandler("init_test", s =>
            {
                Initializer.AddStep(("Testing!", () => { Thread.Sleep(3000); }
                ));
            }, "test: add dummy init step");
            RegisterHandler("gc", _ => GC.Collect(), "Call GC.Collect");
        }

        private static string Format(object? obj)
        {
            if (obj is string)
                return $"\"{obj}\"";
            if (obj is null)
                return "<null>";
            if (obj is ICollection coll)
                return $"<list,{coll.Count}>";
            if (obj is IEnumerable enumerable)
                return $"<list,{enumerable.Cast<object>().Count()}>";
            return obj.ToString() ?? "<object>";
        }
    }
}
