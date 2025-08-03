#!/usr/bin/env dotnet

// Hey! This file is NOT a source code of the game, but a tool/script to convert models to lightweight SMDL format.
// You should run this file using ' dotnet run ./GenerateSoundMap.cs [path_to_model]' command in the terminal from the /Assets folder.
// :)
// Btw this requires .NET 10(preview 4+), but if you can run the game, you should have it installed already.

#:package AssimpNet@4.*

using System;
using System.IO;
using Assimp;

var importFile = new AssimpContext().ImportFile(args[0], PostProcessSteps.GenerateNormals | PostProcessSteps.Triangulate | PostProcessSteps.FlipUVs)!;
if (importFile == null!)
{
    Console.WriteLine("Failed to import file.");
    return;
}

var e = Path.ChangeExtension(args[0], "smdl");
ExportToSmdl(importFile, importFile.RootNode.Name ?? "Unnamed", e);

Console.WriteLine($"\nExported to {e}");

return;

void ExportToSmdl(Scene scene, string name, string outputPath)
{
    const short version = 1;

    if (scene.MeshCount == 0)
        throw new Exception("Scene has no meshes.");

    Mesh mesh = scene.Meshes[0];

    using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
    using var writer = new BinaryWriter(fs);

    Console.WriteLine($"Version: {version}");
    writer.Write("SMDL"); // header
    writer.Write(version); // version

    Console.WriteLine($"Model name: {name}");
    writer.Write(name); // model name

    // Vertices
    Console.WriteLine($"\nVertex count: {mesh.VertexCount}");
    writer.Write(mesh.VertexCount);
    foreach (var v in mesh.Vertices)
    {
        writer.Write(v.X);
        writer.Write(v.Y);
        writer.Write(v.Z);

        Console.Write($"[{v.X} {v.Y} {v.Z}] ");
    }

    // Normals
    Console.WriteLine($"\n\nNormal count: {mesh.Normals.Count}");
    writer.Write(mesh.Normals.Count);
    foreach (var n in mesh.Normals)
    {
        writer.Write(n.X);
        writer.Write(n.Y);
        writer.Write(n.Z);
        Console.Write($"[{n.X} {n.Y} {n.Z}] ");
    }

    // UVs (only first channel assumed)
    if (mesh.TextureCoordinateChannels[0].Count > 0)
    {
        Console.WriteLine($"\n\nUV count: {mesh.TextureCoordinateChannels[0].Count}");
        writer.Write(mesh.TextureCoordinateChannels[0].Count);
        foreach (var uv in mesh.TextureCoordinateChannels[0])
        {
            writer.Write(uv.X);
            writer.Write(uv.Y);
            Console.Write($"[{uv.X} {uv.Y}] ");
        }
    }
    else
    {
        writer.Write(0); // no uvs
        Console.WriteLine("No UVs present for this model.");
    }
        

    int[] flatIndices = mesh.Faces.SelectMany(f => f.Indices).ToArray();
    writer.Write(flatIndices.Length);
    foreach (var i in flatIndices)
        writer.Write(i);

    

    if (scene.Textures.Any())
    {
        var tex = scene.Textures.First();
        File.WriteAllBytes(outputPath + ".png", tex.CompressedData);
        Console.Write($"\nTexture exported to {outputPath+".png"}");
    }
    return;
    // Not implemented
    if (scene.Textures.Any())
    {
        var tex = scene.Textures.First();
        writer.Write(tex.CompressedData.Length);
        writer.Write(tex.CompressedData);
    }
    else
    {
        writer.Write(0); // no textures
        Console.WriteLine("No textures present for this model.");
    }
}