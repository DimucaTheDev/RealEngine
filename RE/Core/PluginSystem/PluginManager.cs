using System.Reflection;
using Serilog;

namespace RE.Core.PluginSystem
{
    internal class PluginManager
    {
        public static IReadOnlyList<Plugin> Plugins { get; private set; } = [];

        public static void ResolvePlugins()
        {
            Log.Information("Resolving assemblies...");

            List<Plugin> plugins = [];

            foreach (var dll in Directory.GetFiles("DLL\\PLUGINS").Select(Path.GetFullPath))
            {
                Assembly assembly;
                try
                {
                    assembly = Assembly.LoadFrom(dll);

                    var pluginTypes = assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(Plugin)) && !t.IsAbstract);
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

            Plugins = SortPlugins(plugins);

            foreach (var plugin in Plugins)
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

            Log.Information("Loaded {PluginCount} plugins: {@Plugins}", Plugins.Count, Plugins.Select(s => $"{s.PluginInformation.Name} ({s.PluginInformation.Version})"));
        }

        public static void UnloadPlugins()
        {
            foreach (var plugin in Plugins.Reverse())
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
            Plugins = [];
        }

        static List<Plugin> SortPlugins(List<Plugin> plugins)
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
