using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace RE.BuildTask
{
    internal class AssetArchiveManager
    {
        public class Config
        {
            public string Default { get; set; }
            public List<string> Ignore { get; set; }
            public Dictionary<string, List<string>> Cases { get; set; }
        }

        private static TaskLoggingHelper Log;

        public static void Process(string rootFolder, string buildFolder, Config config, TaskLoggingHelper log)
        {
            Log = log;

            var ignore = new HashSet<string>(config.Ignore ?? new(), StringComparer.OrdinalIgnoreCase);
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var pack in config.Cases)
            {
                string archivePath = Path.Combine(buildFolder, pack.Key);

                using var zip = ZipFile.Open(archivePath, ZipArchiveMode.Create);
                Log.LogMessage(MessageImportance.High, $"Creating pak: {archivePath}");

                foreach (var item in pack.Value)
                {
                    string fullPath = Path.Combine(rootFolder, item);

                    if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
                        continue;

                    used.Add(item);

                    AddPathToZip(zip, fullPath, item);
                }
            }

            string defaultArchivePath = Path.Combine(buildFolder, config.Default);

            Log.LogMessage(MessageImportance.High, $"Creating pak: {defaultArchivePath}");
            using (var zip = ZipFile.Open(defaultArchivePath, ZipArchiveMode.Create))
            {
                foreach (string path in Directory.GetFileSystemEntries(rootFolder))
                {
                    string name = Path.GetFileName(path);

                    if (ignore.Contains(name))
                        continue;

                    if (used.Contains(name))
                        continue;

                    if (name.EndsWith(".pak", StringComparison.OrdinalIgnoreCase))
                        continue;

                    AddPathToZip(zip, path, name);
                }
            }
        }

        private static void AddPathToZip(ZipArchive zip, string sourcePath, string entryName)
        {
            if (File.Exists(sourcePath))
            {
                zip.CreateEntryFromFile(sourcePath, entryName, CompressionLevel.Optimal);
                return;
            }

            if (Directory.Exists(sourcePath))
            {
                foreach (string file in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
                {
                    string relative = Path.GetRelativePath(sourcePath, file);
                    string zipPath = Path.Combine(entryName, relative).Replace('\\', '/');

                    zip.CreateEntryFromFile(file, zipPath, CompressionLevel.Optimal);
                }
            }
        }
    }
}
