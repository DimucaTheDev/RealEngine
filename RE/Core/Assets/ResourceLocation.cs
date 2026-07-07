using System;

namespace RE.Core.Assets
{
    public sealed class ResourceLocation : IEquatable<ResourceLocation>
    {
        public string Prefix { get; private set; } = string.Empty;
        public string CleanPath { get; private set; } = string.Empty;
        public bool HasPrefix => !string.IsNullOrEmpty(Prefix);

        private string _fullPath = string.Empty;

        private ResourceLocation()
        {
        }

        public static ResourceLocation Parse(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            string normalized = path.Replace('\\', '/').Trim('/');

            int colonIndex = normalized.IndexOf(':');

            if (colonIndex == 0)
            {
                throw new ArgumentException("Path can not start with colon.", nameof(path));
            }

            var location = new ResourceLocation();

            if (colonIndex > 0)
            {
                location.Prefix = normalized[..(colonIndex + 1)].ToLowerInvariant();

                location.CleanPath = normalized[(colonIndex + 1)..].TrimStart('/');

                if (location.CleanPath.Contains(':'))
                {
                    throw new ArgumentException(
                        $"Invalid path '{path}'. Only one prefix can be specified.", nameof(path));
                }
            }
            else
            {
                location.CleanPath = normalized;
            }

            location._fullPath = location.HasPrefix
                ? $"{location.Prefix}{location.CleanPath}"
                : location.CleanPath;

            return location;
        }

        public override string ToString() => _fullPath;
        public override bool Equals(object? obj) => obj is ResourceLocation other && Equals(other);

        public bool Equals(ResourceLocation? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(_fullPath, other._fullPath, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode() => string.GetHashCode(_fullPath, StringComparison.OrdinalIgnoreCase);

        public static bool operator ==(ResourceLocation? left, ResourceLocation? right) => Equals(left, right);
        public static bool operator !=(ResourceLocation? left, ResourceLocation? right) => !Equals(left, right);
        public static implicit operator string(ResourceLocation location) => location?.ToString() ?? string.Empty;
        public static implicit operator ResourceLocation(string location) => Parse(location);
    }
}