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
                if (args == null || args.Length < 4)
                {
                    Console.Error.WriteLine("Usage: RE.BuildTask <assetsFolder> <outputFolder> <compressAssets> <genZip> <version>");
                    return 1;
                }

                var assetsDir = args[0];
                var outputDir = args[1];

                var doPakOrNotIDontEvenKnow = args[2] == "true";

                Console.WriteLine($"Working directory: {Environment.CurrentDirectory}");

                if (doPakOrNotIDontEvenKnow)
                {
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
                    ZipFile.CreateFromDirectory(assetsDir, pakPath, CompressionLevel.Optimal,
                        includeBaseDirectory: false);
                    Console.WriteLine("Pak created successfully.");

                    try
                    {
                        Directory.Delete(assetsDir, recursive: true);
                        Console.WriteLine($"Deleted assets directory: {assetsDir}");
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine(
                            $"Warning: could not delete assets directory '{assetsDir}': {ex.Message}");
                    }
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


                if (args[3].ToLower() == "true")
                {
                    var tempFileName = File.OpenWrite(Path.GetTempFileName());
                    ZipFile.CreateFromDirectory(parentDir!, tempFileName, CompressionLevel.SmallestSize, false);

                    var np = parentDir + $"/../RealEngine_{args[4]}_{DateTime.Now:ddMMyy}.zip";
                    tempFileName.Dispose();
                    File.Delete(np);
                    File.Move(tempFileName.Name, np);
                    Console.WriteLine("Zip Build done.");
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
