using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using RE.Core.Scripting.Attributes;
using Serilog;

namespace RE.Core.Scripting
{
    public static partial class CommandHandler
    {
        public class ConsoleCommand
        {
            public required string Name { get; init; }
            public required MethodInfo Method { get; init; }
            public string? Description { get; init; }
        }

        public static readonly Dictionary<string, List<ConsoleCommand>> RegisteredCommands = new();

        public static void RegisterCommandsInAssembly(Assembly assembly)
        {
            foreach (var method in assembly.DefinedTypes
                         .SelectMany(type =>
                             type.GetMethods(BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public))
                         .Where(method => method.GetCustomAttribute<ConsoleCommandAttribute>() is not null))
            {
                var attribute = method.GetCustomAttribute<ConsoleCommandAttribute>()!;
                var name = attribute.Name;

                var command = new ConsoleCommand
                {
                    Name = name,
                    Description = attribute.Description,
                    Method = method,
                };

                if (!RegisteredCommands.TryGetValue(name, out var list))
                {
                    list = new List<ConsoleCommand>();
                    RegisteredCommands[name] = list;
                }

                list.Add(command);

                Log.Debug("Registered command {Command}. Method: {Namespace}.{Class}.{Method}",
                    name,
                    method.DeclaringType!.Namespace, method.DeclaringType.Name, method.Name);
            }
        }

        public static void ExecuteCommand(string fullCommand)
        {
            if (string.IsNullOrWhiteSpace(fullCommand))
                return;

            fullCommand = fullCommand.Trim();

            if (fullCommand.StartsWith("#"))
                return;

            var splitIndex = fullCommand.IndexOf(' ');
            var commandName = splitIndex == -1 ? fullCommand : fullCommand[..splitIndex];

            var commandArgsRaw = splitIndex == -1 ? "" : fullCommand[(splitIndex + 1)..];

            var matches = DqmArgsRegex().Matches(commandArgsRaw);

            var args = new object?[matches.Count];

            for (int i = 0; i < matches.Count; i++)
            {
                string valueRaw = matches[i].Value;

                if (valueRaw.StartsWith("\"") && valueRaw.EndsWith("\""))
                    valueRaw = valueRaw[1..^1];

                args[i] = valueRaw; // todo: add converter string->type 
            }

            var command = ResolveOverload(commandName, args);

            if (command == null)
            {
                Log.Error("No matching overload for {Command} with {ArgCount} args", commandName, args.Length);
                return;
            }

            var parameters = command.Method.GetParameters();
            var typedArgs = new object?[args.Length];

            for (int i = 0; i < args.Length; i++)
            {
                typedArgs[i] = ConvertFromString((string)args[i]!, parameters[i].ParameterType);
            }

            command.Method.Invoke(null, typedArgs);
        }

        public static void ExecuteCommand(string commandName, params object?[]? args)
        {
            var command = ResolveOverload(commandName, args);
            command.Method.Invoke(null, args);
        }

        private static ConsoleCommand ResolveOverload(string name, object?[]? args)
        {
            if (!RegisteredCommands.TryGetValue(name, out var overloads))
                throw new CommandNotFoundException(name);

            foreach (var cmd in overloads)
            {
                var parameters = cmd.Method.GetParameters();

                if (parameters.Length != (args?.Length ?? 0))
                    continue;

                bool match = true;

                for (int i = 0; i < parameters.Length; i++)
                {
                    if (args![i] == null)
                        continue;

                    if (!parameters[i].ParameterType.IsInstanceOfType(args[i]))
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                    return cmd;
            }

            throw new CommandNotFoundException(name);
        }

        internal static void RegisterAllCommands()
        {
            return;
            /*
            RegisterHandler("var", list =>
            {
                //todo: list support for args > 2
                if (list.Count == 2)
                {
                    string key = list[0];
                    object value = list[1];
                    if (float.TryParse(list[1].Replace("f", ""), NumberStyles.Float, CultureInfo.InvariantCulture,
                            out float floatValue))
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
                if (!Variables.GlobalVariables.Any())
                {
                    Log.Information("Nothing to show");
                    return;
                }

                int l = Math.Max(15, Variables.GlobalVariables.Keys.Select(s => s.Length).Max());
                int valueWidth = 15;
                int totalWidth = l + valueWidth + 1;
                string horizontalLine = new string('-', totalWidth);

                Log.Information(horizontalLine);
                Log.Information($"{"NAME".PadRight(l)}|{"VALUE".PadLeft(valueWidth)}");
                Log.Information(new string('-', l) + "+" + new string('-', valueWidth));

                foreach (var variable in Variables.GlobalVariables)
                {
                    Log.Information("{Name}|{Value}", variable.Key.PadRight(l),
                        Format(variable.Value).PadLeft(valueWidth));
                }

                Log.Information(horizontalLine);
            }, "Get all variables in formatted table");
            RegisterHandler("sound", list =>
            {
                if (list.Count == 0)
                {
                    Log.Error("Usage: {Usage}",
                        "sound play <name> [volume] [inWorld] [maxDistance] [referenceDistance]");
                    Log.Error("Usage: {Usage}", "sound stopall");
                    return;
                }

                if (list[0] == "stopall")
                    SoundManager.StopAll();
                if (list[0] == "play") // sound play <name> [volume] [inWorld] [maxDistance] [referenceDistance]
                {
                    throw new NotImplementedException();
                    if (list.Count < 2)
                    {
                        Log.Error("Usage: {Usage}",
                            "sound play <name> [volume] [inWorld] [maxDistance] [referenceDistance]");
                        return;
                    }

                    string name = list[1];
                    float volume = list.Count > 2 ? float.Parse(list[2]) : 1f;
                    bool inWorld = list.Count > 3 && bool.Parse(list[3]);
                    float maxDistance = list.Count > 4 ? float.Parse(list[4]) : 10f;
                    float referenceDistance = list.Count > 5 ? float.Parse(list[5]) : 1f;
                    /*SoundManager.Play(name, new()
                    {
                        Volume = volume,
                        InWorld = inWorld,
                        MaxDistance = maxDistance,
                        ReferenceDistance = referenceDistance
                    });* /
                }
            }, "Play or stop sound. Enter command with no arguments to see more info");

            RegisterSingleArgHandler("echo",
                c => { Log.Information(new string(c[c.IndexOf(' ')..].SkipWhile(s => s == ' ').ToArray())); },
                "Prints to log with Info level");
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
                    //SceneEditor.Instance?.Disable();
                    var name = args[0];
                    if (!ContentManager.Exists($"assets/maps/{name}/data.json"))
                    {
                        Log.Error("File not found: {FilePath}", $"assets/maps/{name}/data.json");
                        return;
                    }

                    Log.Information("Loading {Level}... ", name);
                    SceneManager.LoadScene(SceneManager.LoadFromMapFile(name), true,
                        () => { SoundManager.PlayOneShotEvent("event:/Flash"); });
                }
            }, "Print level name or load a new one");
            RegisterHandler("editor", args =>
            {
                var ov = SceneEditor.Instance;
                if (!SceneEditor.Enabled)
                    ov.Enable();
                else
                    Log.Error("Editor can be closed only via Editor -> Exit");
                //   ov.Disable();
            }, "Starts the scene editor for current level");
            RegisterHandler("credits", _ => { WinApi.MessageBox(0, "Made with ❤️ by DimucaTheDev", "About", 0x40); });
            RegisterSingleArgHandler("debug",
                s => { DebugOverlay.Instance.IsVisible = !DebugOverlay.Instance.IsVisible; },
                "Open or close debug overlay");
            RegisterSingleArgHandler("init_test", s =>
            {
                Initializer.AddStep(new InitializingTask
                {
                    Label = "Testing!",
                    Action = () => { Thread.Sleep(3000); }
                });
            }, "test: add dummy init step");
            RegisterHandler("gc", _ => GC.Collect(), "Call GC.Collect");
            RegisterHandler("vsync", args =>
            {
                if (args.Count == 0)
                {
                    Log.Information("V-Sync {State}", Game.Instance.VSync == VSyncMode.Off ? "OFF" : "ON");
                    return;
                }

                if (args[0] is not ("enable" or "disable"))
                {
                    Log.Error("Usage: {Usage}", "vsync enable|disable");
                    return;
                }

                Game.Instance.VSync = args[0] == "enable" ? VSyncMode.On : VSyncMode.Off;
                Log.Information("V-Sync {State}", Game.Instance.VSync == VSyncMode.Off ? "OFF" : "ON");
            }, "Enable or disable V-Sync");
            RegisterHandler("fps", list => { Game.Instance.UpdateFrequency = int.Parse(list.First()); },
                "Set max frame rate");
            RegisterSingleArgHandler("test_1", _ =>
            {
                void SpawnCube(Vector3 p)
                {
                    GameObject g = new();
                    g.Components.Add(new RigidBodyComponent(0.5f));
                    g.Components.Add(new BoxColliderComponent());
                    g.Components.Add(new MeshComponent("assets/models/crate.fbx"));
                    SceneManager.CurrentScene.GameObjects.Add(g);
                    g.SetPosition(p);
                }

                Vector3 start = new Vector3(7f, 1.3f, -3f);

                int baseCount = 6;
                float step = 2.3f;

                int c = 0;
                for (int row = 0; row < baseCount; row++)
                {
                    int count = baseCount - row;

                    float zOffset = row * step;
                    float xStart = row * (step / 2f);

                    for (int i = 0; i < count; i++)
                    {
                        c++;
                        Vector3 pos = new Vector3(
                            start.Z + xStart + i * step,
                            start.Y + row * step + 0.2f,
                            start.X
                            //  start.X + zOffset
                        );

                        Time.Schedule(100 + 150 * (c), () => SpawnCube(pos));
                    }
                }
            });
            RegisterHandler("bo", e =>
                {
                    var m = Enum.Parse<DebugDrawModes>(e.First(), true);
                    BulletDebugDrawer.Mode = m;
                    Log.Information($"{nameof(BulletDebugDrawer.Mode)} = {{Value}}", m);
                }, $"Set Bullet overlay. Possible values: {string.Join(", ", Enum.GetNames(typeof(DebugDrawModes)))}");
    */
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

        //remove or rewrite or extend or whatever 🙏🙏🙏🙏🙏🙏🙏🙏
        private static object ConvertFromString(string value, Type type)
        {
            if (type == typeof(string))
                return value;

            if (type == typeof(int))
                return int.Parse(value);

            if (type == typeof(float))
                return float.Parse(value, CultureInfo.InvariantCulture);

            if (type == typeof(bool))
                return bool.Parse(value);

            if (type == typeof(object))
                return value;

            throw new NotSupportedException($"Converting to {type} from string is not supported");
        }

        [GeneratedRegex("""[\"].+?[\"]|\S+""")]
        private static partial Regex DqmArgsRegex();
    }
}