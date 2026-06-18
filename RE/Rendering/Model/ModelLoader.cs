using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Assimp;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using RE.Core.Assets;
using RE.Rendering;
using RE.Rendering.Lighting;
using RE.Rendering.Model;
using RE.Rendering.Renderables.ModelFormat;
using RE.Rendering.Texturing;
using Serilog;
using StbImageSharp;
using Material = RE.Rendering.Lighting.Material;

namespace RE.Utils;

public static class ModelLoader
{
    public static readonly Func<string, ModelData> DefaultModelCacheFactory = LoadFromFile;

    public static ModelData LoadFromFile(string path)
    {
        if (path.EndsWith(".smdl", true, CultureInfo.InvariantCulture))
        {
            var model = StaticModelParser.LoadModel(path);
            var m = ProcessSmdlModel(model, path);
            Log.Verbose("{FileName}: verts={Vertices} ind={Indices} size={Size}",
                Path.GetFileName(path),
                m.Mesh.Vertices.Count,
                m.Mesh.Indices.Count,
                80L + m.Mesh.Vertices.Capacity * 12L + m.Mesh.Indices.Capacity * 4L);

            return m;
        }

        var assimp = ProcessAssimpModel(path);

        Log.Verbose("{FileName}: verts={Vertices} ind={Indices} size={Size}",
            Path.GetFileName(path),
            assimp.Mesh.Vertices.Count,
            assimp.Mesh.Indices.Count,
            80L + assimp.Mesh.Vertices.Capacity * 12L + assimp.Mesh.Indices.Capacity * 4L);

        return assimp;
    }

    private static ModelData ProcessAssimpModel(string path)
    {
        using AssimpContext importer = new AssimpContext();

        Scene scene = importer.ImportFileFromStream(ContentManager.Open(path),
            PostProcessSteps.Triangulate | PostProcessSteps.GenerateNormals | PostProcessSteps.FlipUVs);
        
        if (!scene.Meshes.Any())
            throw new("No meshes found in model");

        var min = new Vector3D(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3D(float.MinValue, float.MinValue, float.MinValue);

        foreach (var mesh in scene.Meshes)
        {
            for (int i = 0; i < mesh.VertexCount; i++)
            {
                var v = mesh.Vertices[i];
                min.X = Math.Min(min.X, v.X);
                min.Y = Math.Min(min.Y, v.Y);
                min.Z = Math.Min(min.Z, v.Z);

                max.X = Math.Max(max.X, v.X);
                max.Y = Math.Max(max.Y, v.Y);
                max.Z = Math.Max(max.Z, v.Z);
            }
        }

        var maxBounds = max.ToOpenTkVector3();
        var minBounds = min.ToOpenTkVector3();
        var center = (min + max) * 0.5f;

        OpenTK.Mathematics.Quaternion correctionRotation =
            OpenTK.Mathematics.Quaternion.FromAxisAngle(Vector3.UnitX, MathHelper.DegreesToRadians(-90.0f));

        List<float> renderVertices = new();
        List<Vector3> vertices = new();
        List<uint> indices = new();
        uint vertexOffset = 0;

        foreach (var mesh in scene.Meshes)
        {
            for (int i = 0; i < mesh.VertexCount; i++)
            {
                var pos = mesh
                    .Vertices[i]; // - center; //for some friki reason all objects are centered even without -center
                Vector3 opentkPos = new Vector3(pos.X, pos.Y, pos.Z);
                opentkPos = Vector3.Transform(opentkPos, correctionRotation);
                vertices.Add(opentkPos);

                var uv = mesh.HasTextureCoords(0)
                    ? mesh.TextureCoordinateChannels[0][i]
                    : new Vector3D(0, 0, 0);

                var normal = mesh.HasNormals
                    ? mesh.Normals[i].ToOpenTkVector3()
                    : new Vector3(0, 0, 1);

                normal = Vector3.Transform(normal, correctionRotation);

                renderVertices.AddRange(new[]
                {
                    opentkPos.X, opentkPos.Y, opentkPos.Z,
                    uv.X, uv.Y,
                    normal.X, normal.Y, normal.Z
                });
            }

            foreach (var face in mesh.Faces)
            {
                foreach (var index in face.Indices)
                {
                    indices.Add((uint)(index + vertexOffset));
                }
            }

            vertexOffset += (uint)mesh.VertexCount;
        }

        var vao = (uint)GL.GenVertexArray();
        var vbo = (uint)GL.GenBuffer();
        var ebo = (uint)GL.GenBuffer();

        GL.BindVertexArray(vao);

        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, renderVertices.Count * sizeof(float),
            renderVertices.ToArray(), BufferUsageHint.StaticDraw);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Count * sizeof(uint),
            indices.ToArray(), BufferUsageHint.StaticDraw);

        
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 0);
        GL.EnableVertexAttribArray(0);

        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 8 * sizeof(float),
            3 * sizeof(float));
        GL.EnableVertexAttribArray(1);

        GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float),
            5 * sizeof(float));
        GL.EnableVertexAttribArray(2);

        var material = new Material();
        material.Texture = GetOrLoadTexture(path, scene);

        ModelData data = new()
        {
            Mesh = new ModelMesh()
            {
                Vao = vao,
                Vbo = vbo,
                Ebo = ebo,
                IndexCount = indices.Count,
                MinBounds = minBounds,
                MaxBounds = maxBounds,
                Indices = indices,
                Vertices = vertices
            },
            Material = material,
            AssetPath = path
        };

        return data;
    }

    private static ModelData ProcessSmdlModel(StaticModelData model, string path)
    {
        var mesh = new ModelMesh();

        var vao = (uint)GL.GenVertexArray();
        var vbo = (uint)GL.GenBuffer();
        var ebo = (uint)GL.GenBuffer();

        var min = new Vector3D(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3D(float.MinValue, float.MinValue, float.MinValue);

        foreach (var v in model.Vertices)
        {
            min.X = Math.Min(min.X, v.X);
            min.Y = Math.Min(min.Y, v.Y);
            min.Z = Math.Min(min.Z, v.Z);

            max.X = Math.Max(max.X, v.X);
            max.Y = Math.Max(max.Y, v.Y);
            max.Z = Math.Max(max.Z, v.Z);
        }

        var maxBounds = max.ToOpenTkVector3();
        var minBounds = min.ToOpenTkVector3();

        mesh.Vao = vao;
        mesh.Vbo = vbo;
        mesh.Ebo = ebo;
        mesh.IndexCount = model.Indices.Length;
        mesh.Indices = model.Indices.ToList();
        mesh.Vertices = model.Vertices.ToList();
        mesh.MinBounds = minBounds;
        mesh.MaxBounds = maxBounds;

        var vertexCount = model.Vertices.Length;

        var vertices = new Vertex[vertexCount];

        for (int i = 0; i < vertexCount; i++)
        {
            vertices[i] = new Vertex
            {
                Position = model.Vertices[i],
                UV = model.UVs != null && model.UVs.Length > i ? model.UVs[i] : Vector2.Zero,
                Normal = model.Normals != null && model.Normals.Length > i ? model.Normals[i] : Vector3.UnitZ
            };
        }

        GL.BindVertexArray(vao);

        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * Unsafe.SizeOf<Vertex>(), vertices,
            BufferUsageHint.StaticDraw);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, model.Indices.Length * sizeof(uint), model.Indices,
            BufferUsageHint.StaticDraw);

        int stride = Unsafe.SizeOf<Vertex>();

// position
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
        GL.EnableVertexAttribArray(0);

// uv
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride,
            Marshal.OffsetOf<Vertex>(nameof(Vertex.UV)));
        GL.EnableVertexAttribArray(1);

// normal
        GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, stride,
            Marshal.OffsetOf<Vertex>(nameof(Vertex.Normal)));
        GL.EnableVertexAttribArray(2);

        var material = new Material
        {
            Texture = GetOrLoadTexture(path)
        };

        ModelData data = new()
        {
            Mesh = mesh,
            Material = material,
            AssetPath = path
        };

        return data;
    }

    private static Texture GetOrLoadTexture(string path, Scene? scene = null)
    {
        if (path.EndsWith(".smdl", true, CultureInfo.InvariantCulture))
        {
            if (!ContentManager.Exists(path + ".png"))
                return StaticTexture.CreateMissingTexture();

            var readAllBytes = ContentManager.GetBytes(path + ".png");
            var t = ImageResult.FromMemory(readAllBytes, ColorComponents.RedGreenBlueAlpha);
            var lt = new StaticTexture(t.Data, t.Width, t.Height);

            return lt;
        }

        Scene importFile = scene!;

        if (scene == null)
        {
            using var assimpContext = new AssimpContext();
            importFile = assimpContext.ImportFileFromStream(ContentManager.Open(path));
        }


        var mat = importFile.Materials.FirstOrDefault();
        string? texPath = mat?.TextureDiffuse.FilePath;
        StaticTexture texId;

        if (texPath != null && ContentManager.Exists(texPath))
        {
            texId = AssetCache.Get(texPath, StaticTexture.DefaultCacheFactory);
        }
        else if ((mat?.HasTextureDiffuse ?? false) && importFile.Textures.Any())
        {
            var t = ImageResult.FromMemory(importFile.Textures.First().CompressedData,
                ColorComponents.RedGreenBlueAlpha);
            //cache?
            texId = new StaticTexture(t.Data, t.Width, t.Height);
        }
        else
        {
            Log.Warning("No staticTexture for {Path}", path);
            texId = StaticTexture.CreateMissingTexture();
        }

        importFile.Clear();
        return texId;
    }

    private struct Vertex
    {
        public Vector3 Position;
        public Vector2 UV;
        public Vector3 Normal;
    }
}