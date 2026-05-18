using System.Globalization;
using Assimp;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using RE.Core.Assets;
using RE.Rendering;
using RE.Rendering.Renderables.ModelFormat;
using Material = RE.Rendering.Lighting.Material;
using ModelData = (RE.Rendering.ModelMesh Mesh, RE.Rendering.Lighting.Material Material);

namespace RE.Utils;

public static class ModelLoader
{
    public static readonly Func<string, ModelData> DefaultCacheFactory = LoadFromFile;

    public static ModelData LoadFromFile(string path)
    {
        if (path.EndsWith(".smdl", true, CultureInfo.InvariantCulture))
        {
            var model = StaticModelParser.LoadModel(path);

            return ProcessSmdlModel(model);
        }

        return ProcessAssimpModel(path);
    }

    private static ModelData ProcessAssimpModel(string path)
    {
        using AssimpContext importer = new AssimpContext();

        Scene scene = importer.ImportFileFromStream(ContentManager.Open(path),
            PostProcessSteps.Triangulate | PostProcessSteps.GenerateNormals | PostProcessSteps.FlipUVs);

        if (!scene.Meshes.Any())
            throw new("No meshes found in model");

        var mesh = scene.Meshes[0];

        var min = new Vector3D(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3D(float.MinValue, float.MinValue, float.MinValue);

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

        var maxBounds = max.ToOpenTkVector3();
        var minBounds = min.ToOpenTkVector3();

        var center = (min + max) * 0.5f;

        OpenTK.Mathematics.Quaternion correctionRotation =
            OpenTK.Mathematics.Quaternion.FromAxisAngle(Vector3.UnitX, MathHelper.DegreesToRadians(-90.0f));

        List<float> renderVertices = new();

        for (int i = 0; i < mesh.VertexCount; i++)
        {
            var pos = mesh.Vertices[i] - center;
            Vector3 opentkPos = new Vector3(pos.X, pos.Y, pos.Z);

            opentkPos = Vector3.Transform(opentkPos, correctionRotation);

            var uv = mesh.HasTextureCoords(0)
                ? mesh.TextureCoordinateChannels[0][i]
                : new Vector3D(0, 0, 0);

            var normal = mesh.HasNormals ? mesh.Normals[i].ToOpenTkVector3() : new(0, 0, 1);

            normal = Vector3.Transform(normal,
                OpenTK.Mathematics.Quaternion.FromAxisAngle(Vector3.UnitX, MathHelper.DegreesToRadians(-90.0f)));
            renderVertices.AddRange([
                opentkPos.X, opentkPos.Y, opentkPos.Z, uv.X, uv.Y, normal.X, normal.Y, normal.Z
            ]);
            //physicsVerticesTemp.AddRange([opentkPos.X, opentkPos.Y, opentkPos.Z]);
        }


        List<int> indices = new();

        foreach (var face in mesh.Faces)
            indices.AddRange(face.Indices);

        var vao = (uint)GL.GenVertexArray();
        var vbo = (uint)GL.GenBuffer();
        var ebo = (uint)GL.GenBuffer();

        GL.BindVertexArray(vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, renderVertices.Count * sizeof(float), renderVertices.ToArray(),
            BufferUsageHint.StaticDraw);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Count * sizeof(uint), indices.ToArray(),
            BufferUsageHint.StaticDraw);

        // pos
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 0);
        GL.EnableVertexAttribArray(0);

        // uv
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 8 * sizeof(float),
            3 * sizeof(float));
        GL.EnableVertexAttribArray(1);

        // normals
        GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float),
            5 * sizeof(float));
        GL.EnableVertexAttribArray(2);

        ModelData data = new()
        {
            Mesh = new ModelMesh()
            {
                Vao = vao,
                Vbo = vbo,
                Ebo = ebo,
                IndexCount = indices.Count,
                MinBounds = minBounds,
                MaxBounds = maxBounds
            },
            Material = new Material()
        };

        return data;
    }

    private static ModelData ProcessSmdlModel(StaticModelData model)
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
        mesh.MinBounds = minBounds;
        mesh.MaxBounds = maxBounds;

        GL.BindVertexArray(vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, model.Vertices.Length * sizeof(float), model.Vertices,
            BufferUsageHint.StaticDraw);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, model.Indices.Length * sizeof(uint), model.Indices,
            BufferUsageHint.StaticDraw);

        // pos
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 0);
        GL.EnableVertexAttribArray(0);

        // uv
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 8 * sizeof(float),
            3 * sizeof(float));
        GL.EnableVertexAttribArray(1);

        // normals
        GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float),
            5 * sizeof(float));
        GL.EnableVertexAttribArray(2);

        ModelData data = new()
        {
            Material = new Material(),
            Mesh = mesh
        };

        return data;
    }
}