using Serilog;

namespace RE.Core.Assets
{
    public enum AssetType
    {
        Texture,
        Model,
        Audio,
        Shader
    }
    public class AssetManager
    {
        //todo: remove class
        public static List<DynamicAsset> LoadedAssets = [];

        public static bool PreLoad(string asset)
        {
            var ext = Path.GetExtension(asset).ToLowerInvariant();

            var type = ext switch
            {
                ".png" or ".jpg" or ".jpeg" => AssetType.Texture,
                ".wav" or ".mp3" or ".ogg" => AssetType.Audio,
                ".smdl" or ".fbx" or ".obj" or ".glb" => AssetType.Model,
                ".vert" or ".frag" => AssetType.Shader,
                _ => throw new NotSupportedException("Unsupported asset type: " + asset)
            };

            return PreLoad(asset, type);
        }
        public static bool PreLoad(string asset, AssetType type)
        {
            Log.Debug("Preloading {Asset}", asset);
            if (!File.Exists(asset))
            {
                Log.Error("Asset file does not exist: {Asset}", asset);
                return false;
            }


            return true;
        }

        public static void UnloadAll()
        {
            foreach (var asset in LoadedAssets.ToList())
            {
                asset.OnUnload();
            }
        }
    }
}
