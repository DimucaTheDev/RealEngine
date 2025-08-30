using Serilog;

namespace RE.Core.Assets
{
    public abstract class DynamicAsset
    {
        protected DynamicAsset(string path)
        {
            AssetPath = path;
            AssetManager.LoadedAssets.Add(this);
            Log.Verbose($"  new dyn.asset:  {GetType().Name}<{GetHashCode()}> {(!string.IsNullOrWhiteSpace(AssetPath) ? $"///\t{AssetPath}" : "")}");
        }
        public string? AssetPath { get; }

        public virtual void OnLoad() { }

        public virtual void OnUnload()
        {
            Log.Verbose($"  dyn.asset unload: {GetType().Name}<{GetHashCode()}> {(!string.IsNullOrWhiteSpace(AssetPath) ? $"///\t{AssetPath}" : "")}");
            AssetManager.LoadedAssets.Remove(this);
        }
    }
}
