using Serilog;
using Serilog.Core;

namespace RE.Core.PluginSystem
{
    public abstract class Plugin
    {
        public ILogger Logger { get; protected set; }

        protected Plugin()
        {
            // ReSharper disable once VirtualMemberCallInConstructor
            Logger = Log.Logger.ForContext("SourceContext", Path.GetFileName(PluginInformation.Assembly.Location));
        }

        public abstract PluginInfo PluginInformation { get; }

        public virtual void OnLoad() { }
        public virtual void OnUnload() { }
    }
}
