namespace RE.Core.PluginSystem
{
    public abstract class Plugin
    {
        public abstract PluginInfo PluginInformation { get; }

        public virtual void OnLoad() { }
        public virtual void OnUnload() { }
    }
}
