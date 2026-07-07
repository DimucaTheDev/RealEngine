namespace RE.Core.Assets;

public static class AssetCache
{
    private static readonly Dictionary<string, DynamicAsset> Cache = new();

    public static T Get<T>(ResourceLocation path, Func<string, T> factory) where T : DynamicAsset
    {
        var normalizePath = ContentManager.NormalizePath(path);
        if (Cache.TryGetValue(normalizePath, out var result))
        {
            return result as T ??
                   throw new InvalidOperationException($"Cached asset of type {result.GetType().Name} can not be converted to {typeof(T).FullName}");
        }

        var asset = factory(normalizePath);
        Cache.Add(normalizePath, asset);
        return asset;
    }
}