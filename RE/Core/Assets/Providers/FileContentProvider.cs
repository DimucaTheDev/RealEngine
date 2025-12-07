namespace RE.Core.Assets.Providers
{
    public class FileContentProvider : IContentProvider
    {
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
        public Stream Open(string path)
        {
            return File.OpenRead(path);
        }

        public string Prefix => "file:";
    }
}