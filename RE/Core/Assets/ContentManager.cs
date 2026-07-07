using System.Diagnostics.CodeAnalysis;
using RE.Core.Assets.Providers;
using Serilog;

namespace RE.Core.Assets
{
    public static class ContentManager
    {
        public static IContentProvider Default { get; set; } = null!;
        public static bool ResolveAssetsInAllContentProviders => true;

        private static readonly List<IContentProvider> _providers = new();
        private static readonly Lock _lock = new();

        public static void Register(IContentProvider provider)
        {
            ArgumentNullException.ThrowIfNull(provider);

            if (string.IsNullOrWhiteSpace(provider.Prefix))
                throw new ArgumentException("Provider prefix cannot be null or empty.", nameof(provider));

            lock (_lock)
            {
                if (_providers.Exists(p => p.Prefix.Equals(provider.Prefix, StringComparison.OrdinalIgnoreCase)))
                    throw new ArgumentException(
                        $"A provider with the prefix '{provider.Prefix}' is already registered.", nameof(provider));

                try
                {
                    provider.Register();
                    _providers.Add(provider);
                    Log.Information("Registered content provider {Name} ('{Prefix}').", provider.GetType().Name,
                        provider.Prefix);

                    if (Default == null!)
                    {
                        Default = provider;
                        Log.Debug("Default content provider is {Name} ('{Prefix}').", provider.GetType().Name,
                            provider.Prefix);
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error occurred while registering content provider {Name}.", provider.GetType().Name);
                }
            }
        }

        public static bool Exists([NotNullWhen(true)] ResourceLocation? path)
        {
            if (path == null)
                return false;
            var provider = FindProvider(path);
            return provider != null && provider.Exists(path);
        }

        public static string GetString(ResourceLocation path)
        {
            var provider = FindProvider(path)
                           ?? throw new InvalidOperationException(
                               $"No matching content provider found for {path} and Default is not set.");

            Log.Verbose("Provider for {Path} is {Provider}", path, provider.GetType().Name);

            if (!provider.Exists(path))
                throw new FileNotFoundException($"Asset not found: {path}");

            return provider.GetString(path);
        }

        public static byte[] GetBytes(ResourceLocation path)
        {
            var provider = FindProvider(path)
                           ?? throw new InvalidOperationException(
                               $"No matching content provider found for {path} and Default is not set.");

            if (!provider.Exists(path))
                throw new FileNotFoundException($"Asset not found: {path}");

            return provider.GetBytes(path);
        }

        public static byte[] GetBytes(ResourceLocation path, int offset, int count)
        {
            var provider = FindProvider(path)
                           ?? throw new InvalidOperationException(
                               $"No matching content provider found for {path} and Default is not set.");

            if (!provider.Exists(path))
                throw new FileNotFoundException($"Asset not found: {path}");

            return provider.GetBytes(path, offset, count);
        }

        public static Stream Open(ResourceLocation path)
        {
            var provider = FindProvider(path)
                           ?? throw new InvalidOperationException(
                               $"No matching content provider found for {path} and Default is not set.");

            if (!provider.Exists(path))
                throw new FileNotFoundException($"Asset not found: {path}");

            return provider.Open(path);
        }

        public static ResourceLocation[] GetFiles(ResourceLocation path, bool recursive = false)
        {
            var provider = FindProvider(path)
                           ?? throw new InvalidOperationException(
                               $"No matching content provider found for {path} and Default is not set.");

            return provider.GetFiles(path, recursive)
                .Select(p => NormalizePath(p, provider))
                .ToArray();
        }

        public static ResourceLocation[] GetFiles(ResourceLocation path, string extension, bool recursive = false)
        {
            return GetFiles(path, recursive)
                .Where(s => s.CleanPath.EndsWith(extension, StringComparison.InvariantCultureIgnoreCase))
                .ToArray();
        }

        public static ResourceLocation[] GetDirectories(ResourceLocation path, bool recursive = false)
        {
            var provider = FindProvider(path)
                           ?? throw new InvalidOperationException(
                               $"No matching content provider found for {path} and Default is not set.");

            return provider.GetDirectories(path, recursive)
                .Select(p => NormalizePath(p, provider))
                .ToArray();
        }

        [return: NotNullIfNotNull("path")]
        public static ResourceLocation? NormalizePath(ResourceLocation? path, IContentProvider? provider = null)
        {
            if (path == null)
                return null;

            if (path.HasPrefix)
            {
                return path;
            }

            provider ??= FindProvider(path);
            if (provider == null)
                return path;

            return ResourceLocation.Parse($"{provider.Prefix}{path.CleanPath}");
        }

        private static IContentProvider? FindProvider(ResourceLocation path)
        {
            if (!path.HasPrefix && ResolveAssetsInAllContentProviders)
            {
                return _providers.FirstOrDefault(s => s.Exists(path) || s.DirectoryExists(path)) ?? Default;
            }

            if (path.HasPrefix)
            {
                foreach (var p in _providers)
                {
                    if (path.Prefix.Equals(p.Prefix, StringComparison.OrdinalIgnoreCase))
                        return p;
                }
            }

            return Default;
        }
    }
}