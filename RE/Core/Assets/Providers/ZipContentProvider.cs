using System.IO.Compression;
using RE.Utils;
using Serilog;

namespace RE.Core.Assets.Providers
{
    /// <summary>
    /// Provides access to files and directories stored within ZIP archives located in the "paks" directory, using a
    /// virtual file system interface.
    /// </summary>
    /// <remarks>The <see cref="ZipContentProvider"/> enables applications to retrieve, enumerate, and check the existence
    /// of files and directories within ZIP archives as if they were part of a unified content source. All ZIP files in
    /// the "paks" directory are loaded and indexed when <see cref="Register"/> is called. Paths are resolved relative to the "Assets"
    /// directory and are case-insensitive. This provider is typically used to support modding or asset packaging
    /// scenarios where content is distributed in ZIP (PAK) files.</remarks>
    public class ZipContentProvider : IContentProvider
    {
        private readonly Dictionary<string, string> _fileMap = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ZipArchive> _zips = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ZipArchiveEntry> _fastFileMap = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _directoryCache = new(StringComparer.OrdinalIgnoreCase);

        public string Prefix => "pak:";

        /// <summary>
        /// Scans the "paks" directory for archive files and registers their contents for later access.
        /// </summary>
        /// <remarks>This method loads all files in the "paks" directory as ZIP archives and indexes their
        /// entries for lookup. If duplicate file names are found across different archives, a warning is logged. This
        /// method should be called before attempting to access files managed by the archive system.</remarks>
        public void Register()
        {
            Directory.CreateDirectory("paks");
            foreach (var pakPath in Directory.EnumerateFiles("paks", "*.pak"))
            {
                try
                {
                    var fs = File.OpenRead(pakPath);
                    var zip = new ZipArchive(fs, ZipArchiveMode.Read, leaveOpen: false);
                    _zips[pakPath] = zip;

                    RegisterEntries(zip, pakPath);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to load PAK: {PakPath}", pakPath);
                }
            }

            Log.Information("Loaded PAKs: {@Paks}", _zips.Keys.Select(pack => Path.GetRelativePath(".", pack)));
        }

        private void RegisterEntries(ZipArchive zip, string pakPath)
        {
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\"))
                {
                    var dirPath = entry.FullName.Replace('\\', '/').TrimEnd('/');
                    RegisterDirectoryPath(dirPath);
                    continue;
                }

                var normalized = entry.FullName.Replace('\\', '/');

                if (!_fastFileMap.TryAdd(normalized, entry))
                {
                    Log.Warning("Duplicate file {FileName} in PAKs. Existing: {OldPak}", normalized, _fileMap.GetValueOrDefault(normalized));
                }
                else
                {
                    _fileMap[normalized] = pakPath;
                }

                var directoryPath = Path.GetDirectoryName(normalized)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(directoryPath))
                {
                    RegisterDirectoryPath(directoryPath);
                }
            }
        }

        private void RegisterDirectoryPath(string directoryPath)
        {
            var parts = directoryPath.Split('/');
            string currentPath = "";
            foreach (var part in parts)
            {
                currentPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}/{part}";
                _directoryCache.Add(currentPath);
            }
        }

        public byte[] GetBytes(string path)
        {
            var entry = GetFileEntry(path);
            if (entry == null)
                throw new FileNotFoundException($"File not found in any pak: {path}");

            using var stream = entry.Open();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        public byte[] GetBytes(string path, int offset, int count)
        {
            var entry = GetFileEntry(path);
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
            return GetFileEntry(path) != null;
        }
        private ZipArchiveEntry? GetFileEntry(string path)
        {
            var normalized = NormalizePath(path);
            return _fastFileMap.GetValueOrDefault(normalized);
        }

        private bool InternalDirectoryExists(string path)
        {
            var normalized = NormalizePath(path);
            return _directoryCache.Contains(normalized);
        }

        public bool DirectoryExists(string path) => InternalDirectoryExists(path);

        public string[] GetDirectories(string path, bool recursive = false)
        {
            var normalized = NormalizePath(path);
            var prefix = string.IsNullOrEmpty(normalized) ? "" : normalized + "/";

            return _directoryCache
                .Where(d => d.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Where(d =>
                {
                    if (recursive)
                        return true;
                    var relative = d[prefix.Length..];
                    return !relative.Contains('/');
                })
                .Select(d => "Assets/" + d)
                .ToArray();
        }

        private string NormalizePath(string path)
        {
            path = path.Replace('\\', '/').TrimEnd('/');

            path = path.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
                ? path[Prefix.Length..] : path;

            path = path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                ? path["Assets/".Length..] : path;
             
            return path;
        }
        public Stream Open(string path)
        {
            var entry = GetFileEntry(path);
            if (entry == null)
                throw new FileNotFoundException($"File not found in any pak: {path}");

            return entry.Open().AsMemoryStream();
        }
        public string[] GetFiles(string path, bool recursive = false)
        {
            string normalizedPath = NormalizePath(path);
            if (!string.IsNullOrEmpty(normalizedPath) && !normalizedPath.EndsWith("/"))
            {
                normalizedPath += "/";
            }

            var entries = _zips.Values.SelectMany(zip => zip.Entries);

            return entries
                .Where(entry =>
                {
                    string fullName = entry.FullName.Replace('\\', '/');

                    if (!fullName.StartsWith(normalizedPath, StringComparison.OrdinalIgnoreCase))
                        return false;

                    if (!recursive)
                    {
                        string relativePath = fullName.Substring(normalizedPath.Length);
                        return !relativePath.Contains('/');
                    }

                    return true;
                })
                .Select(s => Prefix + "Assets/" + s.FullName)
                .ToArray();
        }
    }
}
