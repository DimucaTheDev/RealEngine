using System.Text;

namespace RE.Core.Assets.Providers
{
    public interface IContentProvider
    {
        public byte[] GetBytes(string path, int offset, int count);
        public byte[] GetBytes(string path);
        public string GetString(string path)
        {
            return Encoding.UTF8.GetString(GetBytes(path));
        }
        public bool Exists(string path);
        public bool DirectoryExists(string path);
        public Stream Open(string path);
        public string[] GetFiles(string path, bool recursive = false);
        public string[] GetDirectories(string path, bool recursive = false);

        public void Register() { }

        public string Prefix { get; }
    }
}
