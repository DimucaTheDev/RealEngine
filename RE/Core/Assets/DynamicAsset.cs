using Serilog;

namespace RE.Core.Assets
{
    /// <summary>
    /// Represents a dynamically managed asset that can be loaded, created and unloaded at runtime.
    /// </summary>
    /// <remarks>This is an abstract base class for assets that are tracked by the asset management system.
    /// Derived types should implement specific asset behavior and may override the OnLoad and OnUnload methods to
    /// provide custom loading and unloading logic.</remarks>
    public abstract class DynamicAsset
    {
        public string? AssetPath { get; }

        protected DynamicAsset() : this(null!) { }
        protected DynamicAsset(string path)
        {
            AssetPath = path;
            AssetManager.LoadedAssets.Add(this);
            if (string.IsNullOrWhiteSpace(AssetPath))
                Log.Verbose("  new dyn.asset:  {Name}<{HashCode}>", GetType().Name, GetHashCode());
            else
                Log.Verbose("  new dyn.asset:  {Name}<{HashCode}> ///\t{Path}", GetType().Name, GetHashCode(), AssetPath);
        }

        public virtual void OnLoad() { }

        public virtual void OnUnload()
        {
            if (string.IsNullOrWhiteSpace(AssetPath))
                Log.Verbose("  dyn.asset unload:  {Name}<{HashCode}>", GetType().Name, GetHashCode());
            else
                Log.Verbose("  dyn.asset unload:  {Name}<{HashCode}> ///\t{Path}", GetType().Name, GetHashCode(), AssetPath);
            AssetManager.LoadedAssets.Remove(this);
        }
    }
}
