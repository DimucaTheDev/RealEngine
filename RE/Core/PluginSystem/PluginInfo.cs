using System.Reflection;

namespace RE.Core.PluginSystem
{
    /// <summary>
    /// Represents metadata information about a plugin, including its name, version, author, and related assembly.
    /// </summary>
    /// <remarks>Use this class to store and access descriptive details about a plugin, such as its display
    /// name, version, author, and optional dependencies. The information provided by this class can be used for plugin
    /// discovery, display in user interfaces, or managing plugin loading order.</remarks>
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
