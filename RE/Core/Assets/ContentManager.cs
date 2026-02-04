using System.Text;
using RE.Core.Assets.Providers;
using Serilog;

namespace RE.Core.Assets
{
    /// <summary>
    /// Provides centralized access to registered content providers and helpers to load assets
    /// (strings, bytes and streams) by path. Providers are identified by a prefix and can be
    /// registered at runtime. If an asset path does not include a provider prefix the manager
    /// can either try all registered providers or resolve against the default provider.
    /// </summary>
    public static class ContentManager
    {
        private static readonly List<IContentProvider> _providers = new();

        /// <summary>
        /// The default content provider used when a path does not contain a provider prefix
        /// and no other provider can be resolved.
        /// </summary>
        public static IContentProvider Default { get; set; } = null!;

        /// <summary>
        /// Should the manager search for assets in all registered content providers when
        /// the requested path does not include a provider prefix. When <c>true</c> the
        /// manager will probe each provider for existence of the asset or directory.
        /// When <c>false</c> the manager will only resolve providers by explicit prefix
        /// or use the <see cref="Default"/> provider.
        /// </summary>
        public static bool ResolveAssetsInAllContentProviders { get; set; } = true;

        private static readonly Lock _lock = new();

        /// <summary>
        /// Registers a content provider so it can be used by the manager. The provider
        /// must specify a non-empty <see cref="IContentProvider.Prefix"/> and will be
        /// initialized via its <see cref="IContentProvider.Register"/> method.
        /// </summary>
        /// <param name="provider">The content provider to register.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the provider prefix is null/empty or a provider
        /// with the same prefix is already registered.</exception>
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

        /// <summary>
        /// Checks whether an asset exists for the given path. The path may include a
        /// provider prefix or, if no prefix is present, the manager's resolution policy
        /// determines which provider(s) are queried.
        /// </summary>
        /// <param name="path">The asset path to check.</param>
        /// <returns><c>true</c> if the asset exists; otherwise <c>false</c>.</returns>
        public static bool Exists(string path)
        {
            var provider = FindProvider(path);
            return provider != null && provider.Exists(path);
        }

        /// <summary>
        /// Loads the asset at the specified path and returns its contents decoded as UTF-8 text.
        /// </summary>
        /// <param name="path">The asset path to load.</param>
        /// <returns>The UTF-8 decoded string content of the asset.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no matching provider can be resolved
        /// and <see cref="Default"/> is not set.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the asset does not exist.</exception>
        public static string GetString(string path)
        {
            var provider = FindProvider(path)
                ?? throw new InvalidOperationException($"No matching content provider found and {nameof(Default)} is not set.");

            if (!provider.Exists(path))
                throw new FileNotFoundException($"Asset not found: {path}");

            return Encoding.UTF8.GetString(provider.GetBytes(path));
        }

        /// <summary>
        /// Loads the asset at the specified path and returns its contents as a byte array.
        /// </summary>
        /// <param name="path">The asset path to load.</param>
        /// <returns>The raw bytes of the asset.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no matching provider can be resolved
        /// and <see cref="Default"/> is not set.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the asset does not exist.</exception>
        public static byte[] GetBytes(string path)
        {
            var provider = FindProvider(path)
                ?? throw new InvalidOperationException("No matching content provider found and Default is not set.");

            if (!provider.Exists(path))
                throw new FileNotFoundException($"Asset not found: {path}");

            return provider.GetBytes(path);
        }

        /// <summary>
        /// Loads a portion of the asset at the specified path and returns the requested bytes.
        /// </summary>
        /// <param name="path">The asset path to load.</param>
        /// <param name="offset">The zero-based byte offset in the asset at which to begin reading.</param>
        /// <param name="count">The number of bytes to read.</param>
        /// <returns>The requested bytes from the asset.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no matching provider can be resolved
        /// and <see cref="Default"/> is not set.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the asset does not exist.</exception>
        public static byte[] GetBytes(string path, int offset, int count)
        {
            var provider = FindProvider(path)
                ?? throw new InvalidOperationException("No matching content provider found and Default is not set.");

            if (!provider.Exists(path))
                throw new FileNotFoundException($"Asset not found: {path}");

            return provider.GetBytes(path, offset, count);
        }

        /// <summary>
        /// Opens a read-only stream to the asset at the specified path.
        /// </summary>
        /// <param name="path">The asset path to open.</param>
        /// <returns>A <see cref="Stream"/> to read the asset data.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no matching provider can be resolved
        /// and <see cref="Default"/> is not set.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the asset does not exist.</exception>
        public static Stream Open(string path)
        {
            var provider = FindProvider(path)
                ?? throw new InvalidOperationException("No matching content provider found and Default is not set.");

            if (!provider.Exists(path))
                throw new FileNotFoundException($"Asset not found: {path}");

            return provider.Open(path);
        }

        /// <summary>
        /// Returns the file entries under the specified path from the resolved provider.
        /// </summary>
        /// <param name="path">The directory path to enumerate.</param>
        /// <param name="recursive">Whether to include files from subdirectories.</param>
        /// <returns>An array of file paths available under the specified path.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no matching provider can be resolved
        /// and <see cref="Default"/> is not set.</exception>
        public static string[] GetFiles(string path, bool recursive = false)
        { 
            var provider = FindProvider(path)
                ?? throw new InvalidOperationException("No matching content provider found and Default is not set.");
            var f = provider.GetFiles(path, recursive);
            return f;
        }

        public static string[] GetFiles(string path, string extension, bool recursive = false)
        {
            return GetFiles(path, recursive).Where(s => s.EndsWith(extension, StringComparison.InvariantCultureIgnoreCase)).ToArray();
        }

        /// <summary>
        /// Returns the directory entries under the specified path from the resolved provider.
        /// </summary>
        /// <param name="path">The directory path to enumerate.</param>
        /// <param name="recursive">Whether to include directories from subdirectories.</param>
        /// <returns>An array of directory paths available under the specified path.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no matching provider can be resolved
        /// and <see cref="Default"/> is not set.</exception>
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