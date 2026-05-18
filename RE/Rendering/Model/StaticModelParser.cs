using System.Diagnostics;
using System.Text;
using OpenTK.Mathematics;
using RE.Core.Assets;

namespace RE.Rendering.Renderables.ModelFormat
{
    /// <summary>
    /// This class is responsible for loading static 3D models from a custom binary format <c>SMDL</c>.
    /// </summary>
    public static class StaticModelParser
    {
        /// <summary>
        /// Attempts to load a static model from the specified file path.
        /// </summary>
        /// <param name="modelPath">Path to SMDL file. Path is relative to working directory.</param>
        /// <param name="data">Deserialized model data</param>
        /// <returns>Exception occured during loading process. Can be <see langword="null"/></returns>
        /// <exception cref="InvalidDataException">Provided file is not valid SMDL file</exception>
        /// <exception cref="NotSupportedException">File version is not supported by Engine</exception>
        public static StaticModelData LoadModel(string modelPath)
        {
            using var reader = new BinaryReader(ContentManager.Open(modelPath), Encoding.UTF8, false);
            if (reader.ReadString() != "SMDL") //file header
            {
                throw new InvalidDataException("The file is not a valid Static Model.");
            }

            switch (reader.ReadInt16())
            {
                case 1:
                    return Version1(reader);
                default:
                    throw new NotSupportedException("Unsupported Static Model version.");
            }
        }

        public static bool TryLoadModel(string modelPath, out StaticModelData? data)
        {
            try
            {
                return (data = LoadModel(modelPath)) != null;
            }
            catch
            {
                if (Debugger.IsAttached)
                    throw;
            }

            return (data = null) == null; // how convenient!
        }

        private static StaticModelData Version1(BinaryReader reader)
        {
            StaticModelData data = new();
            data.Name = reader.ReadString();
            
            var vertexCount = reader.ReadInt32();
            var vertices = new Vector3[vertexCount];
            for (var i = 0; i < vertexCount; i++)
            {
                vertices[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            }

            var normalCount = reader.ReadInt32();
            var normals = new Vector3[normalCount];
            for (var i = 0; i < normalCount; i++)
            {
                normals[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            }

            var uvCount = reader.ReadInt32();
            var uvs = new Vector2[uvCount];
            for (var i = 0; i < uvCount; i++)
            {
                uvs[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            }

            var indexCount = reader.ReadInt32();
            var faces = new int[indexCount];
            for (var i = 0; i < indexCount; i++)
                faces[i] = reader.ReadInt32();


            data.Vertices = vertices;
            data.Normals = normals;
            data.UVs = uvs;
            data.Indices = faces;
            return data;
        }
    }

    public class StaticModelData
    {
        public string Name { get; set; }
        public Vector3[] Vertices { get; set; }
        public Vector3[] Normals { get; set; }
        public Vector2[] UVs { get; set; }
        public int[] Indices { get; set; }
    }
}