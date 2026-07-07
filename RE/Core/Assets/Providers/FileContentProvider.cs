namespace RE.Core.Assets.Providers
{
    /// <summary>
    /// <see cref="IContentProvider"/> implementation that uses <see cref="File"/> class for retrieving data.
    /// </summary>
    public class FileContentProvider : IContentProvider
    {
        public string Prefix => "file:";

        public byte[] GetBytes(ResourceLocation path, int offset, int count)
        {
            using var fs = File.OpenRead(path.CleanPath);
            byte[] buffer = [];
            fs.ReadExactly(buffer, offset, count);
            return buffer;
        }

        public byte[] GetBytes(ResourceLocation path)
        {
            return File.ReadAllBytes(path.CleanPath);
        }

        public string GetString(ResourceLocation path)
        {
            return File.ReadAllText(path.CleanPath);
        }

        public bool Exists(ResourceLocation path)
        {
            return File.Exists(path.CleanPath);
        }

        public bool DirectoryExists(ResourceLocation path)
        {
            return Directory.Exists(path.CleanPath);
        }

        public Stream Open(ResourceLocation path)
        {
            return File.OpenRead(path.CleanPath);
        }

        public ResourceLocation[] GetFiles(ResourceLocation path, bool recursive = false)
        {
            return Directory
                .GetFiles(path.CleanPath, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .Select(ResourceLocation.Parse)
                .ToArray();
        }

        public ResourceLocation[] GetDirectories(ResourceLocation path, bool recursive = false)
        {
            return Directory
                .GetDirectories(path.CleanPath, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .Select(ResourceLocation.Parse)
                .ToArray();
        }
    }
}