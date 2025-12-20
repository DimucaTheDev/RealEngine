using System;
using System.IO;
using System.IO.Compression;

namespace RE.BuildTask
{
    internal static class Program
    {
        static int Main(string[] args)
        {
            try
            {
                if (args == null || args.Length < 2)
                {
                    Console.Error.WriteLine("Usage: RE.BuildTask <assetsFolder> <outputFolder>");
                    return 1;
                }

                var assetsDir = args[0];
                var outputDir = args[1];

                Console.WriteLine($"Working directory: {Environment.CurrentDirectory}");
                Console.WriteLine($"Assets folder: {assetsDir}");
                Console.WriteLine($"Output folder: {outputDir}");

                if (!Directory.Exists(assetsDir))
                {
                    Console.Error.WriteLine($"Assets folder does not exist: {assetsDir}");
                    return 2;
                }

                Directory.CreateDirectory(outputDir);

                var pakPath = Path.Combine(outputDir, "assets.pak");

                if (File.Exists(pakPath))
                {
                    Console.WriteLine($"Removing existing pak: {pakPath}");
                    File.Delete(pakPath);
                }

                Console.WriteLine($"Creating pak: {pakPath}");
                ZipFile.CreateFromDirectory(assetsDir, pakPath, CompressionLevel.Optimal, includeBaseDirectory: false);
                Console.WriteLine("Pak created successfully.");

                try
                {
                    Directory.Delete(assetsDir, recursive: true);
                    Console.WriteLine($"Deleted assets directory: {assetsDir}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Warning: could not delete assets directory '{assetsDir}': {ex.Message}");
                }

                var assetsFull = Path.GetFullPath(assetsDir);
                var parentDir = Path.GetDirectoryName(assetsFull) ?? Environment.CurrentDirectory;
                var targetDllDir = Path.Combine(parentDir, "dll", "WIN32");
                Directory.CreateDirectory(targetDllDir);

                Console.WriteLine($"Looking for DLLs in: {parentDir}");
                var dlls = Directory.GetFiles(parentDir, "*.dll", SearchOption.TopDirectoryOnly);

                foreach (var dll in dlls)
                {
                    try
                    {
                        var dest = Path.Combine(targetDllDir, Path.GetFileName(dll));
                        Console.WriteLine($"{dll} -> {dest}");
                        if (File.Exists(dest))
                        {
                            File.Delete(dest);
                        }
                        File.Move(dll, dest);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Failed to move '{dll}': {ex.Message}");
                    }
                }

                Console.WriteLine("Done.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Unhandled error: {ex}");
                return -1;
            }
        }
    }
}
