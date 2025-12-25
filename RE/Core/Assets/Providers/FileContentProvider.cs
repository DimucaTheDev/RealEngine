namespace RE.Core.Assets.Providers
{
    /// <summary>
    /// <see cref="IContentProvider"/> implementation that uses <see cref="File"/> class for retrieving data.
    /// </summary>
    public class FileContentProvider : IContentProvider
    {
        public string Prefix => "file:";

        public byte[] GetBytes(string path, int offset, int count)
        {
            using var fs = File.OpenRead(path);
            byte[] buffer = [];
            fs.ReadExactly(buffer, offset, count);
            return buffer;
        }
        public byte[] GetBytes(string path)
        {
            return File.ReadAllBytes(path);
        }
        public string GetString(string path)
        {
            return File.ReadAllText(path);
        }
        public bool Exists(string path)
        {
            return File.Exists(path);
        }
        public bool DirectoryExists(string path)
        {
            return Directory.Exists(path);
        }
        public Stream Open(string path)
        {
            return File.OpenRead(path);
        }
        public string[] GetFiles(string path, bool recursive = false)
        {
            return Directory.GetFiles(path, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
        }
        public string[] GetDirectories(string path, bool recursive = false)
        {
            return Directory.GetDirectories(path, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
        }
    }
}