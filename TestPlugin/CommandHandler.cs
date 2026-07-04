using RE.Core.Scripting.Attributes;
using Serilog;

namespace TestPlugin;

public static class CommandHandler
{
    [ConsoleCommand(Name = "plugin_test", Description = "Example command from loaded assembly")]
    public static void TestCommand()
    {
        Log.Information("Executed command from plugin!");
    }
}