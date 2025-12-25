using Serilog.Core;

namespace RE.Core.PluginSystem
{
    /// <summary>
    /// Provides a base class for implementing plugins that can be loaded and unloaded by the host application.
    /// </summary>
    /// <remarks>Inherit from this class to create a custom plugin. Override the <see cref="OnLoad"/> and <see
    /// cref="OnUnload"/> methods to define initialization and cleanup logic for your plugin. Use the <see
    /// cref="Logger"/> property to write log messages within the plugin context. The <see cref="PluginInformation"/>
    /// property should provide metadata about the plugin, such as its name and version.</remarks>
    public abstract class Plugin
    {
        /// <summary>
        /// Logger instance for logging messages within the plugin context.
        /// </summary>
        // public ILogger Logger { get; protected set; }

        protected Plugin()
        {
            // ReSharper disable once VirtualMemberCallInConstructor
          //  Logger = Log.Logger.ForContext("SourceContext", Path.GetFileName(PluginInformation.Assembly.Location));
        }

        /// <summary>
        /// Gets the plugin's metadata information.
        /// </summary>
        public abstract PluginInfo PluginInformation { get; }

        /// <summary>
        /// Called when plugin is loaded. Override to implement custom initialization logic.
        /// </summary>
        public virtual void OnLoad() { }
        /// <summary>
        /// Called when plugin is unloaded. Override to implement custom cleanup logic.
        /// </summary>
        public virtual void OnUnload() { }
    }
}
