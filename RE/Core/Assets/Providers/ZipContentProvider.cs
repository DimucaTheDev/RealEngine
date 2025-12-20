using System.IO.Compression;
using RE.Utils;
using Serilog;

namespace RE.Core.Assets.Providers
{
    internal class ZipContentProvider : IContentProvider
    {
        private readonly Dictionary<string, string> _fileMap = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ZipArchive> _zips = new(StringComparer.OrdinalIgnoreCase);

        public void Register()
        {
            foreach (var pakPath in Directory.EnumerateFiles("paks"))
            {
                var fs = File.OpenRead(pakPath);
                var zip = new ZipArchive(fs, ZipArchiveMode.Read, leaveOpen: false);
                _zips[pakPath] = zip;

                foreach (var entry in zip.Entries)
                {
                    var normalized = entry.FullName.Replace('\\', '/');

                    if (entry.FullName.EndsWith("/"))
                        continue; // is a directory, skip

                    if (!_fileMap.TryAdd(normalized, pakPath))
                    {
                        Log.Error("Duplicate file {FileName} in: {Pak1} and {Pak2}", normalized, _fileMap[normalized], pakPath);
                    }
                }
            }

            Log.Information("Loaded PAKs: {@Paks}", _zips.Keys.Select(pack => Path.GetRelativePath(".", pack)));
        }

        public byte[] GetBytes(string path)
        {
            var entry = GetEntry(path);
            if (entry == null)
                throw new FileNotFoundException($"File not found in any pak: {path}");

            using var stream = entry.Open();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        public byte[] GetBytes(string path, int offset, int count)
        {
            var entry = GetEntry(path);
            if (entry == null)
                throw new FileNotFoundException($"File not found in any pak: {path}");

            using var stream = entry.Open();
            stream.Seek(offset, SeekOrigin.Begin);

            var buffer = new byte[count];
            int read = stream.Read(buffer, 0, count);

            if (read < count)
                Array.Resize(ref buffer, read);

            return buffer;
        }

        public bool Exists(string path)
        {
            return GetEntry(path) != null;
        }

        public Stream Open(string path)
        {
            var entry = GetEntry(path);
            if (entry == null)
                throw new FileNotFoundException($"File not found in any pak: {path}");

            return entry.Open().AsMemoryStream();
        }

        public string Prefix => "pak:";

        private ZipArchiveEntry? GetEntry(string path)
        {
            var normalized = Path.GetRelativePath("Assets", path).Replace('\\', '/');
            if (!_fileMap.TryGetValue(normalized, out var pakPath))
                return null;

            var zip = _zips[pakPath];
            return zip.Entries.FirstOrDefault(e =>
                string.Equals(e.FullName.Replace('\\', '/'), normalized, StringComparison.OrdinalIgnoreCase));
        }
    }
}
