using System.Collections;
using System.Configuration;
using System.Dynamic;
using System.Globalization;
using BulletSharp;
using OpenTK.Windowing.Common;
using RE.Core.Assets;
using RE.Core.Audio;
using RE.Core.Logging;
using RE.Core.Scripting.Attributes;
using RE.Core.World;
using RE.Core.World.Components.Physics;
using RE.Editor;
using RE.Utils;
using Serilog;

namespace RE.Core.Scripting;

internal class CommonEngineCommands
{
    [ConsoleCommand(Name = "help", Description = "Shows this help")]
    static void Help()
    {
        Log.Information("Available commands:");
        foreach (var command in CommandHandler.RegisteredCommands
                     .SelectMany(c => c.Value))
        {
            Log.Information(" - {Command}({Params}): {Description}",
                command.Name,
                string.Join(", ", command.Method.GetParameters().Select(s => $"{s.ParameterType.Name} {s.Name}")),
                command.Description);
        }
    }

    [ConsoleCommand(Name = "var", Description = "Get all variables")]
    static void AllVars()
    {
        if (!Variables.GlobalVariables.Any())
        {
            Log.Information(@"¯\_(ツ)_/¯");
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
    }


    [ConsoleCommand(Name = "var", Description = "Get variable")]
    static void GetVar(string varName)
    {
        Log.Information("{Name} = {Value}", varName, Format(Variables.GetVariable(varName)));
    }

    [ConsoleCommand(Name = "var", Description = "Set variable")]
    static void SetVar(string varName, string strValue)
    {
        object value = strValue;
        if (float.TryParse(strValue.Replace("f", ""), NumberStyles.Float, CultureInfo.InvariantCulture,
                out float floatValue))
            value = floatValue;
        else if (bool.TryParse(strValue, out bool boolValue))
            value = boolValue;
        else if (strValue.Equals("null", StringComparison.OrdinalIgnoreCase))
            value = null!;
        Variables.SetVariable(varName, value);
    }

    [ConsoleCommand(Name = "clear", Description = "Clears the console")]
    static void Clear()
    {
        GameLogger.Log.Clear();
    }

    [ConsoleCommand(Name = "exit", Description = "Shutdowns the application")]
    static void Exit()
    {
        Game.Instance.Close();
    }

    [ConsoleCommand(Name = "source", Description = "Executes the script")]
    static void Source(string scriptPath)
    {
        if (!ContentManager.Exists(scriptPath))
        {
            Log.Error("File not found: {FilePath}", scriptPath);
            return;
        }

        string src = ContentManager.GetString(scriptPath);
        foreach (var line in src.Split('\n'))
        {
            CommandHandler.ExecuteCommand /*Safe*/(line);
        }
    }

    [ConsoleCommand(Name = "scene", Description = "Print current scene name")]
    static void Scene()
    {
        Log.Information(SceneManager.CurrentScene?.Name ?? "Scene not loaded");
    }

    [ConsoleCommand(Name = "scene", Description = "Loads the scene")]
    static void LoadScene(string name)
    {
        //SceneEditor.Instance?.Disable();
        if (!ContentManager.Exists($"assets/maps/{name}/data.json"))
        {
            Log.Error("File not found: {FilePath}", $"assets/maps/{name}/data.json");
            return;
        }

        Log.Information("Loading {Scene}... ", name);
        SceneManager.LoadScene(
            SceneSerializer.DeserializeScene(ContentManager.GetString($"assets/maps/{name}/data.json")), true,
            () => { SoundManager.PlayOneShotEvent("event:/Flash"); });
    }

    [ConsoleCommand(Name = "editor", Description = "Launches Scene Editor")]
    static void Editor()
    {
        var ov = SceneEditor.Instance;
        if (!SceneEditor.Enabled)
            ov.Enable();
        else
            //Log.Error("Editor can be closed only via Editor -> Exit");
            ov.Disable();
    }

    [ConsoleCommand(Name = "vsync", Description = "Set VSync mode")]
    static void Vsync(string state)
    {
        if (state is not ("enable" or "disable"))
        {
            Log.Error("Usage: {Usage}", "vsync enable|disable");
            return;
        }

        Game.Instance.VSync = state == "enable" ? VSyncMode.On : VSyncMode.Off;
        Log.Information("V-Sync {State}", Game.Instance.VSync == VSyncMode.Off ? "OFF" : "ON");
    }

    [ConsoleCommand(Name = "bo", Description = "Set bullet overlay")]
    static void Bo(string overlay)
    {
        var m = Enum.Parse<DebugDrawModes>(overlay, true);
        BulletDebugDrawer.Mode = m;
        Log.Information($"{nameof(BulletDebugDrawer.Mode)} = {{Value}}", m);
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