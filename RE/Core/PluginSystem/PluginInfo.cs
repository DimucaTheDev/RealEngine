using System.Reflection;

namespace RE.Core.PluginSystem
{
    /// <summary>
    /// todo: XML docs
    /// </summary>
    public class PluginInfo
    {
        public required string Name { get; set; }
        public Version? Version { get; set; }
        public string? Author { get; set; }
        public string? Description { get; set; }
        public string? LoadBefore { get; set; }
        public Assembly Assembly { get; } = Assembly.GetCallingAssembly();
    }
}
