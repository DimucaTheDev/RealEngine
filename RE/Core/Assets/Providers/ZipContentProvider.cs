using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using RE.Utils;
using Serilog;

namespace RE.Core.Assets.Providers
{
    /// <summary>
    /// Provides access to files and directories stored within ZIP archives located in the "paks" directory, using a
    /// virtual file system interface.
    /// </summary>
    public class ZipContentProvider : IContentProvider
    {
        private readonly Dictionary<string, string> _fileMap = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ZipArchive> _zips = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ZipArchiveEntry> _fastFileMap = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _directoryCache = new(StringComparer.OrdinalIgnoreCase);

        public string Prefix => "pak:";
 
        public void Register()
        {
            Directory.CreateDirectory("Engine/Content");
            foreach (var pakPath in Directory.EnumerateFiles("Engine/Content", "*.pak"))
            {
                try
                {
                    var fs = File.OpenRead(pakPath);
                    var zip = new ZipArchive(fs, ZipArchiveMode.Read, leaveOpen: false);
                    _zips[pakPath] = zip;

                    RegisterEntries(zip, pakPath);
                    Log.Information("Mounted PAK {Path}", Path.GetRelativePath(".", pakPath));
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to mount PAK: {PakPath}", pakPath);
                }
            }
        }

        private void RegisterEntries(ZipArchive zip, string pakPath)
        {
            foreach (var entry in zip.Entries)
            {
                string fullName = entry.FullName.Replace('\\', '/');

                if (fullName.EndsWith("/"))
                {
                    var dirPath = fullName.TrimEnd('/');
                    RegisterDirectoryPath(dirPath);
                    continue;
                }

                var normalized = fullName.ToLowerInvariant();

                if (!_fastFileMap.TryAdd(normalized, entry))
                {
                    Log.Error("Duplicate file {FileName} in PAKs. Existing: {OldPak}", normalized,
                        _fileMap.GetValueOrDefault(normalized));
                }
                else
                {
                    _fileMap[normalized] = pakPath;
                }

                int lastSlash = fullName.LastIndexOf('/');
                if (lastSlash > 0)
                {
                    string directoryPath = fullName[..lastSlash];
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

        public byte[] GetBytes(ResourceLocation path)
        {
            var entry = GetFileEntry(path);
            if (entry == null)
                throw new FileNotFoundException($"File not found in any pak: {path}");

            using var stream = entry.Open();
            using var ms = new MemoryStream((int)entry.Length);
            stream.CopyTo(ms);

            return ms.ToArray();
        }

        public byte[] GetBytes(ResourceLocation path, int offset, int count)
        {
            var entry = GetFileEntry(path);
            if (entry == null)
                throw new FileNotFoundException($"File not found in any pak: {path}");

            using var stream = entry.Open();

            if (offset > 0)
            {
                byte[] skipBuffer = new byte[Math.Min(4096, offset)];
                int remaining = offset;
                while (remaining > 0)
                {
                    int readBytes = stream.Read(skipBuffer, 0, Math.Min(skipBuffer.Length, remaining));
                    if (readBytes <= 0) break;
                    remaining -= readBytes;
                }
            }

            var buffer = new byte[count];
            int totalRead = 0;
            while (totalRead < count)
            {
                int readBytes = stream.Read(buffer, totalRead, count - totalRead);
                if (readBytes <= 0) break;
                totalRead += readBytes;
            }

            if (totalRead < count)
                Array.Resize(ref buffer, totalRead);

            return buffer;
        }

        public bool Exists(ResourceLocation path)
        {
            return GetFileEntry(path) != null;
        }

        public bool DirectoryExists(ResourceLocation path)
        {
            string zipPath = GetZipPath(path);
            return _directoryCache.Contains(zipPath);
        }

        public Stream Open(ResourceLocation path)
        {
            var entry = GetFileEntry(path);
            if (entry == null)
                throw new FileNotFoundException($"File not found in any pak: {path}");

            return entry.Open().AsMemoryStream();
        }

        public ResourceLocation[] GetFiles(ResourceLocation path, bool recursive = false)
        {
            string zipPath = GetZipPath(path);
            if (!string.IsNullOrEmpty(zipPath) && !zipPath.EndsWith("/"))
            {
                zipPath += "/";
            }

            var entries = _zips.Values.SelectMany(zip => zip.Entries);
            var result = new List<ResourceLocation>();

            foreach (var entry in entries)
            {
                string fullName = entry.FullName.Replace('\\', '/');
                if (fullName.EndsWith("/")) continue;

                if (!fullName.StartsWith(zipPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!recursive)
                {
                    string relativePath = fullName[zipPath.Length..];
                    if (relativePath.Contains('/'))
                        continue;
                }

                string virtualPath = $"{Prefix}Assets/{fullName}";
                result.Add(virtualPath);
            }

            return result.ToArray();
        }

        public ResourceLocation[] GetDirectories(ResourceLocation path, bool recursive = false)
        {
            string zipPath = GetZipPath(path);
            string prefix = string.IsNullOrEmpty(zipPath) ? "" : zipPath + "/";
            var result = new List<ResourceLocation>();

            foreach (var d in _directoryCache)
            {
                if (!d.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!recursive)
                {
                    string relative = d[prefix.Length..];
                    if (relative.Contains('/'))
                        continue;
                }

                string virtualPath = $"{Prefix}Assets/{d}";
                result.Add(virtualPath);
            }

            return result.ToArray();
        }

        private ZipArchiveEntry? GetFileEntry(ResourceLocation path)
        {
            string key = GetZipKey(path);
            return _fastFileMap.GetValueOrDefault(key);
        }
 
        private string GetZipPath(ResourceLocation location)
        {
            string clean = location.CleanPath;
            if (clean.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return clean["Assets/".Length..];
            }
            return clean;
        }

        private string GetZipKey(ResourceLocation location)
        {
            return GetZipPath(location).ToLowerInvariant();
        }
    }
}