using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace RE.BuildTask
{
    // ReSharper disable once UnusedType.Global
    public class EngineBuildTask : Task
    {
        [Required] public string AssetsFolder { get; set; } = null!;
        [Required] public string PakOutputFolder { get; set; } = null!;
        public bool CompressAssets { get; set; }
        public bool GenerateZip { get; set; }
        public string Version { get; set; } = "1.0.0";

        public override bool Execute()
        {
            try
            {
                string assetsDir = Path.GetFullPath(AssetsFolder);
                string outputDir = Path.GetFullPath(PakOutputFolder);

                Log.LogMessage(MessageImportance.High, $"RE.BuildTask: Starting processing...");
                Log.LogMessage(MessageImportance.Normal, $"Working directory: {Environment.CurrentDirectory}");

                if (CompressAssets)
                {
                    if (!Directory.Exists(assetsDir))
                    {
                        Log.LogError($"Assets folder does not exist: {assetsDir}");
                        return false;
                    }
                     
                    Directory.CreateDirectory(outputDir);
                    Log.LogMessage(MessageImportance.Normal, $"Created source assets directory: {assetsDir}");

                    string configPath = Path.Combine(assetsDir, "build.pack_info.json");

                    var config = JsonSerializer.Deserialize<AssetArchiveManager.Config>(File.ReadAllText(configPath),
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                    Directory.CreateDirectory(Path.GetDirectoryName(outputDir)!);
                    AssetArchiveManager.Process(assetsDir, outputDir, config, Log);

                    try
                    {
                        Directory.Delete(assetsDir, true);
                        Log.LogMessage(MessageImportance.Normal, $"Deleted source assets directory: {assetsDir}");
                    }
                    catch (Exception ex)
                    {
                        Log.LogWarning($"Could not delete assets directory: {ex.Message}");
                    }
                }

                string parentDir = Path.GetDirectoryName(assetsDir) ?? Environment.CurrentDirectory;
                string targetDllDir = Path.Combine(parentDir, "Engine", "Natives");
                Directory.CreateDirectory(targetDllDir);

                Log.LogMessage(MessageImportance.Normal, $"Organizing DLLs in: {parentDir}");
                var dlls = Directory.GetFiles(parentDir, "*.dll", SearchOption.TopDirectoryOnly);

                foreach (var dll in dlls)
                {
                    try
                    {
                        string fileName = Path.GetFileName(dll);
                        string dest = Path.Combine(targetDllDir, fileName);

                        if (File.Exists(dest))
                            File.Delete(dest);
                        File.Move(dll, dest);
                        Log.LogMessage(MessageImportance.Low, $"Moved: {dll} --> {dest}");
                    }
                    catch (Exception ex)
                    {
                        Log.LogWarning($"Failed to move DLL '{dll}': {ex.Message}");
                    }
                }

                if (GenerateZip)
                {
                    string zipName = $"RealEngine_{Version}_{DateTime.Now:ddMMyy}.zip";
                    string temp = Path.GetTempFileName();
                    File.Delete(temp);
                    string zipPath = Path.Combine(parentDir, zipName);

                    if (File.Exists(zipPath))
                        File.Delete(zipPath);

                    Log.LogMessage(MessageImportance.High, $"Generating final build zip: {zipPath}");
                    ZipFile.CreateFromDirectory(parentDir, temp, CompressionLevel.SmallestSize, false);
                    File.Move(temp, zipPath);
                }

                return !Log.HasLoggedErrors;
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex, true);
                return false;
            }
        }
    }
}