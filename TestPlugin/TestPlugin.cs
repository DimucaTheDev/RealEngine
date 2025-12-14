using RE.Core;
using RE.Core.PluginSystem;
using Serilog;
using Serilog.Core;

namespace TestPlugin
{
    public class TestPlugin : Plugin
    {
        public TestPlugin() => Instance = this;
        public static TestPlugin Instance { get; set; }
        public override PluginInfo PluginInformation { get; } = new PluginInfo
        {
            Name = "Test Plugin",
            Version = new Version(1, 2, 3),
            Author = "Freeman",
            Description = "A test plugin.",
            LoadBefore = null
        };

        public override void OnLoad()
        {
            Log.Information("Loading testing plugin omg!");
            Initializer.AddStep(("plugin loading!", () => Thread.Sleep(100)));
        }
    }
}
