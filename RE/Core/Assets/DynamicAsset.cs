using Serilog;

namespace RE.Core.Assets
{
    public abstract class DynamicAsset
    {
        public string? AssetPath { get; }

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
