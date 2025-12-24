using System.Text;
using System.Windows.Forms.VisualStyles;
using RE.Core.Assets.Providers;
using Serilog;

namespace RE.Core.Assets
{
    public static class ContentManager
    {
        private static readonly List<IContentProvider> _providers = new();

        public static IContentProvider Default { get; set; } = null!;

        /// <summary>
        /// Should manager search for assets in all providers if prefix is not specified.
        /// </summary>
        public static bool ResolveAssetsInAllContentProviders { get; set; } = true;

        private static readonly Lock _lock = new();

        public static void Register(IContentProvider provider)
        {
            ArgumentNullException.ThrowIfNull(provider);

            if (string.IsNullOrWhiteSpace(provider.Prefix))
                throw new ArgumentException("Provider prefix cannot be null or empty.", nameof(provider));

            lock (_lock)
            {
                if (_providers.Exists(p => p.Prefix.Equals(provider.Prefix, StringComparison.OrdinalIgnoreCase)))
                    throw new ArgumentException($"A provider with the prefix '{provider.Prefix}' is already registered.", nameof(provider));

                try
                {
                    provider.Register();
                    _providers.Add(provider);
                    Log.Information("Registered content provider {Name} ('{Prefix}').", provider.GetType().Name, provider.Prefix);
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error occurred while registering content provider {Name}.", provider.GetType().Name);
                }
            }
        }

        private static IContentProvider? ResolveProvider(ref string path)
        {
            foreach (var p in _providers)
            {
                if (path.StartsWith(p.Prefix, StringComparison.OrdinalIgnoreCase))
                {
                    path = path.Substring(p.Prefix.Length);
                    return p;
                }
            }

            return Default;
        }

        public static bool Exists(string path)
        {
            var provider = FindProvider(path);
            return provider != null && provider.Exists(path);
        }

        public static string GetString(string path)
        {
            var provider = FindProvider(path)
                ?? throw new InvalidOperationException($"No matching content provider found and {nameof(Default)} is not set.");

            if (!provider.Exists(path))
                throw new FileNotFoundException($"Asset not found: {path}");

            return Encoding.UTF8.GetString(provider.GetBytes(path));
        }

        public static byte[] GetBytes(string path)
        {
            var provider = FindProvider(path)
                ?? throw new InvalidOperationException("No matching content provider found and Default is not set.");

            if (!provider.Exists(path))
                throw new FileNotFoundException($"Asset not found: {path}");

            return provider.GetBytes(path);
        }

        public static byte[] GetBytes(string path, int offset, int count)
        {
            var provider = FindProvider(path)
                ?? throw new InvalidOperationException("No matching content provider found and Default is not set.");

            if (!provider.Exists(path))
                throw new FileNotFoundException($"Asset not found: {path}");

            return provider.GetBytes(path, offset, count);
        }

        public static Stream Open(string path)
        {
            var provider = FindProvider(path)
                ?? throw new InvalidOperationException("No matching content provider found and Default is not set.");

            if (!provider.Exists(path))
                throw new FileNotFoundException($"Asset not found: {path}");

            return provider.Open(path);
        }

        public static string[] GetFiles(string path, bool recursive = false)
        {
            var provider = FindProvider(path)
                ?? throw new InvalidOperationException("No matching content provider found and Default is not set.");
            return provider.GetFiles(path, recursive);
        }

        public static string[] GetDirectories(string path, bool recursive = false)
        {
            var provider = FindProvider(path)
                ?? throw new InvalidOperationException("No matching content provider found and Default is not set.");
            return provider.GetDirectories(path, recursive);
        }

        private static IContentProvider? FindProvider(string path)
        {
            if (!HasPrefix(path) && ResolveAssetsInAllContentProviders)
                return _providers.FirstOrDefault(s => s.Exists(path) || s.DirectoryExists(path)) ?? Default;

            return ResolveProvider(ref path);
        }
        private static bool HasPrefix(string path)
        {
            foreach (var p in _providers)
            {
                if (path.StartsWith(p.Prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}