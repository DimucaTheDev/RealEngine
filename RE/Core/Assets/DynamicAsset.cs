using JetBrains.Annotations;
using RE.Utils;
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
        /// <summary>
        /// Property that stores the path to the asset file, if applicable.
        /// </summary>
        /// <remarks>
        /// This property may be null or empty for assets that are created entirely in memory and do not correspond to a file.
        /// </remarks>
        public ResourceLocation? AssetPath
        {
            get;
            init => field = ContentManager.NormalizePath(value);
        }

        protected DynamicAsset() : this(null)
        {
        }

        protected DynamicAsset(ResourceLocation? path)
        {
            AssetPath = path;
            if (path != null && !ContentManager.Exists(AssetPath))
                Log.Warning("Specified dynamic asset path doesnt exist: {Path}", AssetPath);

            if (AssetPath != null)
                Log.Verbose("  new dyn.asset:  {Name}<{HashCode}>", GetType().Name, GetHashCode());
            else
                Log.Verbose("  new dyn.asset:  {Name}<{HashCode}> ///\t{Path}", GetType().Name, GetHashCode(),
                    AssetPath);
        }

        public virtual void OnLoad()
        {
        }

        public virtual void OnUnload()
        {
            if (AssetPath == null)
                Log.Verbose("  dyn.asset unload:  {Name}<{HashCode}>", GetType().Name, GetHashCode());
            else
                Log.Verbose("  dyn.asset unload:  {Name}<{HashCode}> ///\t{Path}", GetType().Name, GetHashCode(),
                    AssetPath);
        }
    }
}