using System.Reflection;
using Serilog;

namespace RE.Core.PluginSystem
{
    /// <summary>
    /// Provides static methods and properties for managing the lifecycle of plugins within the application, including
    /// loading, resolving, and unloading plugins.
    /// </summary>
    /// <remarks>The PluginManager class is responsible for discovering plugin assemblies, instantiating
    /// plugin types, and managing their initialization and unloading. All members are static, and the class maintains
    /// the current set of loaded plugins. Thread safety is not guaranteed; callers should ensure appropriate
    /// synchronization if accessing from multiple threads.</remarks>
    public static class PluginManager
    {
        /// <summary>
        /// Gets the list of plugins that are currently loaded by the application.
        /// </summary>
        public static IReadOnlyList<Plugin> LoadedPlugins { get; private set; } = [];

        /// <summary>
        /// Loads and initializes all plugins found in the <c>DLL\PLUGINS</c> directory. This method scans for assemblies,
        /// identifies types that inherit from the <see cref="Plugin"/> base class, and invokes their <see cref="Plugin.OnLoad"/>.
        /// </summary>
        /// <remarks>
        /// Before being loaded, plugins are sorted based on their <see cref="PluginInfo.LoadBefore"/> property to ensure correct load order.
        /// </remarks>
        /// <exception cref="Exception">Thrown if method is unable to load/initialize plugin.</exception>
        /// <exception cref="InvalidOperationException">Thrown if plugin dependencies cycle detected.</exception>
        public static void ResolvePlugins()
        {
            Log.Information("Resolving assemblies...");

            List<Plugin> plugins = [];

            Directory.CreateDirectory("Engine/Plugins");

            foreach (var dll in Directory.GetFiles("Engine/Plugins").Select(Path.GetFullPath))
            {
                Assembly assembly;
                try
                {
                    assembly = Assembly.LoadFrom(dll);

                    var pluginTypes = assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(Plugin)) && !t.IsAbstract).ToList();
                    if (!pluginTypes.Any())
                    {
                        Log.Warning("Assembly does not have any Plugin types: {AssemblyName}", assembly.GetName().Name);
                        continue;
                    }
                    foreach (var type in pluginTypes)
                    {
                        var pluginInstance = (Plugin)Activator.CreateInstance(type)!;
                        plugins.Add(pluginInstance);
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e, "Unable to resolve plugin at {Path}", Path.GetRelativePath(".", dll));
                    continue;
                }
                Log.Information("Resolved assembly {Name}", assembly.GetName().Name);
            }

            LoadedPlugins = SortPlugins(plugins);

            foreach (var plugin in LoadedPlugins)
            {
                try
                {
                    plugin.OnLoad();
                }
                catch (Exception e)
                {
                    Log.Error(e, "Exception occurred when Initializing plugin {PluginName}",
                        plugin.PluginInformation.Name);
                    throw;
                }
            }

            Log.Information("Loaded {PluginCount} plugins: {@LoadedPlugins}", LoadedPlugins.Count,
                LoadedPlugins.Select(s => $"{s.PluginInformation.Name} ({s.PluginInformation.Version})"));
        }

        /// <summary>
        /// Cycles through all currently loaded plugins in reverse and invokes their <see cref="Plugin.OnUnload"/> method.
        /// </summary>
        public static void UnloadPlugins()
        {
            foreach (var plugin in LoadedPlugins.Reverse())
            {
                try
                {
                    Log.Information("Unloading plugin {Plugin}", plugin.PluginInformation.Name);
                    plugin.OnUnload();
                }
                catch (Exception e)
                {
                    Log.Error(e, "Exception occurred when Unloading plugin {PluginName}",
                        plugin.PluginInformation.Name);
                }
            }
            LoadedPlugins = [];
        }

        private static List<Plugin> SortPlugins(List<Plugin> plugins)
        {
            var dict = plugins.ToDictionary(p => p.PluginInformation.Name);
            var edges = new Dictionary<string, List<string>>();

            foreach (var p in plugins)
            {
                if (!edges.ContainsKey(p.PluginInformation.Name))
                    edges[p.PluginInformation.Name] = new List<string>();

                if (!string.IsNullOrEmpty(p.PluginInformation.LoadBefore) && dict.ContainsKey(p.PluginInformation.LoadBefore))
                {
                    // p -> LoadBefore
                    edges[p.PluginInformation.Name].Add(p.PluginInformation.LoadBefore);
                }
            }

            var result = new List<Plugin>();
            var visited = new Dictionary<string, bool>();

            void Dfs(string name)
            {
                if (visited.TryGetValue(name, out var inStack))
                {
                    if (inStack)
                        throw new InvalidOperationException($"Plugin cycle detected: {name}");
                    return;
                }

                visited[name] = true;
                foreach (var dep in edges[name])
                    Dfs(dep);

                visited[name] = false;
                result.Add(dict[name]);
            }

            foreach (var p in plugins)
                Dfs(p.PluginInformation.Name);

            return result;
        }
    }
}
