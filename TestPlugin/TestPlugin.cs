using JetBrains.Annotations;
using RE.Core.PluginSystem;
using Serilog;

namespace TestPlugin
{
    public class TestPlugin : Plugin
    {
        public TestPlugin() => Instance = this;
        public static TestPlugin Instance { get; set; }

        public override PluginInfo PluginInformation { get; } = new()
        {
            Name = "Test Plugin",
            Version = new Version(1, 2, 3),
            Author = "Freeman",
            Description = "A test plugin.",
            LoadBefore = null
        };

        public override void OnLoad()
        {
            Log.Information("Loading testing plugin!");
        }
    }
}