using System.Text;

namespace RE.Core.Assets
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
        public Stream Open(string path);

        public void Register() { }

        public string Prefix { get; }
    }
}
