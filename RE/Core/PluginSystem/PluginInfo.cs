using System.Reflection;

namespace RE.Core.PluginSystem
{
    public class PluginInfo
    {
        public required string Name { get; set; }
        public Version? Version { get; set; }
        public string? Author { get; set; }
        public string? Description { get; set; }
        public string? LoadBefore { get; set; }
        public Assembly Assembly { get; set; } = Assembly.GetCallingAssembly();
    }

}
