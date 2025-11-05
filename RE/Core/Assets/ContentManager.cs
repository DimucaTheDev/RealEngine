using System.Text;
using Serilog;

namespace RE.Core.Assets
{
    public static class ContentManager
    {
        private static readonly List<IContentProvider> _providers = new();
        public static IContentProvider Default { get; set; } = null!;

        public static void Register(IContentProvider provider)
        {
            ArgumentNullException.ThrowIfNull(provider);
            if (string.IsNullOrEmpty(provider.Prefix))
                throw new ArgumentException("Provider prefix cannot be null or empty.", nameof(provider));
            if (_providers.Exists(p => p.Prefix.Equals(provider.Prefix, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException($"A provider with the prefix '{provider.Prefix}' is already registered.", nameof(provider));

            try
            {
                provider.Register();
                _providers.Add(provider);
            }
            catch (Exception e)
            {
                Log.Error(e, "Error occurred while registering content provider {Name}.", provider.GetType().Name);
            }
        }

        private static IContentProvider ResolveProvider(ref string path)
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
            var provider = ResolveProvider(ref path);
            return provider.Exists(path);
        }
        public static string GetString(string path)
        {
            return Encoding.UTF8.GetString(GetBytes(path));
        }
        public static byte[] GetBytes(string path)
        {
            var provider = ResolveProvider(ref path);
            return provider.GetBytes(path);
        }
        public static byte[] GetBytes(string path, int offset, int count)
        {
            var provider = ResolveProvider(ref path);
            return provider.GetBytes(path);
        }
        public static Stream Open(string path)
        {
            var provider = ResolveProvider(ref path);
            return provider.Open(path);
        }
    }
}