using RE.Audio;
using RE.Core.World;
using RE.Rendering;
using Serilog;
using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;

namespace RE.Core.Scripting
{
    internal class CommandHandler
    {
        public static event Action<string, List<string>, string?> CommandExecuted;

        private static int recursionDepth = 0;
        private const int MaxRecursionDepth = 1000;

        public static void ExecuteCommandSafe(string line)
        {
            if (recursionDepth > MaxRecursionDepth)
            {
                Log.Error("Max recursion depth exceeded.");
                return;
            }
            try
            {
                recursionDepth++;
                ExecuteCommand(line);
            }
            finally
            {
                recursionDepth--;
            }
        }
        public static void ExecuteCommand(string command)
        {
            if (command.TrimStart(' ').StartsWith("#") || string.IsNullOrWhiteSpace(command))
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
            ExecuteCommand(result[0], command, result[1..].ToList());
        }
        public static void ExecuteCommand(string command, string full = "", params List<string> args)
        {
            CommandExecuted?.Invoke(command, args, full);
        }
        public static void RegisterHandler(string name, Action<List<string>> handler)
        {
            CommandExecuted += (cmd, args, _) =>
            {
                if (cmd.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    handler(args);
                }
            };
        }
        public static void RegisterSingleArgHandler(string name, Action<string> handler)
        {
            CommandExecuted += (cmd, args, full) =>
            {
                if (cmd.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    handler(full!);
                }
            };
        }

        public static void RegisterAllCommands()
        {
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
                }
                else
                {
                    var value = Variables.GetVariable(list[0]);

                    //todo: return variable value, make Func<> instead of Action<>
                    string content = value != null ? $"{list[0]}:{value.GetType().Name} = " : $"{list[0]} = ";

                    content += Format(value);

                    Log.Information(content);
                }
            });
            RegisterHandler("vars", _ =>
            {
                Log.Information(new string('-', 31));
                Log.Information($"{"NAME",-15}|{"VALUE",15}");
                Log.Information(new string('-', 15) + "+" + new string('-', 15));
                foreach (var variable in Variables.GlobalVariables)
                {
                    Log.Information($"{variable.Key,-15}|{Format(variable.Value),15}");
                }
                Log.Information(new string('-', 31));
            });
            RegisterHandler("clear", _ =>
            {
                GameLogger.Log = "";
            });
            RegisterHandler("sound", list =>
            {
                if (list[0] == "stopall") SoundManager.StopAll();
                if (list[0] == "play") // sound play <name> [volume] [inWorld] [maxDistance] [referenceDistance]
                {
                    if (list.Count < 2)
                    {
                        Log.Error("Usage: sound play <name> [volume] [inWorld] [maxDistance] [referenceDistance]");
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
            });
            RegisterHandler("exit", _ => Game.Instance.Close());
            RegisterHandler("source", list =>
            {
                if (!File.Exists(list[0]))
                {
                    Console.WriteLine(Path.GetFullPath("."));
                    Log.Error($"File not found: {list[0]}");
                    return;
                }
                string src = File.ReadAllText(list[0]);
                foreach (var line in src.Split('\n'))
                {
                    ExecuteCommandSafe(line);
                }
            });
            RegisterSingleArgHandler("echo", c =>
            {
                Log.Information(new string(c[c.IndexOf(' ')..].SkipWhile(s => s == ' ').ToArray()));
            });
            RegisterHandler("frustum", args =>
            {
                if (args[0] == "create") RenderManager.CreateCameraFrustum();
                else if (args[0] == "destroy") RenderManager.RemoveCameraFrustum();
                else Log.Error("Usage: frustum create|destroy");
            });
            RegisterHandler("level", args =>
            {
                if (args.Count == 0) Log.Information($"Current level: {SceneManager.CurrentScene.Name ?? "<unnamed>"}");
                else
                {
                    var name = args[0];
                    if (!File.Exists($"assets/maps/{name}/data.json"))
                    {
                        Log.Error($"File not found: assets/maps/{name}/data.json");
                        return;
                    }
                    Log.Information($"Loading {name}... ");
                    SceneManager.LoadScene(name);
                }
            });
        }

        private static string Format(object? obj)
        {
            if (obj is string) return $"\"{obj}\"";
            if (obj is null) return "<null>";
            if (obj is ICollection coll) return $"<list,{coll.Count}>";
            if (obj is IEnumerable enumerable) return $"<list,{enumerable.Cast<object>().Count()}>";
            return obj.ToString() ?? "<object>";
        }
    }
}
