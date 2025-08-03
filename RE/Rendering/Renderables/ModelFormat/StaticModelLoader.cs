using System.Text;
using OpenTK.Mathematics;

namespace RE.Rendering.Renderables.ModelFormat
{
    internal static class StaticModelLoader
    {
        public static Exception? TryLoadModel(string modelPath, out StaticModelData data)
        {
            try
            {
                using var fileStream = new FileStream(modelPath, FileMode.Open, FileAccess.Read);
                using var reader = new BinaryReader(fileStream, Encoding.UTF8, false);
                if (reader.ReadString() != "SMDL") //file header
                {
                    throw new InvalidDataException("The file is not a valid Static Model.");
                }
                switch (reader.ReadInt16())
                {
                    case 1:
                        data = Version1(reader);
                        return null!;
                    default:
                        throw new NotSupportedException("Unsupported Static Model version.");
                }
            }
            catch (Exception e)
            {
                data = null!;
                return e;
            }
        }
        private static StaticModelData Version1(BinaryReader reader)
        {
            StaticModelData data = new();
            data.Name = reader.ReadString();
            int vertexCount = reader.ReadInt32();
            Vector3[] vertices = new Vector3[vertexCount];
            for (int i = 0; i < vertexCount; i++)
            {
                vertices[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            }
            int normalCount = reader.ReadInt32();
            Vector3[] normals = new Vector3[normalCount];
            for (int i = 0; i < normalCount; i++)
            {
                normals[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            }
            int uvCount = reader.ReadInt32();
            Vector2[] uvs = new Vector2[uvCount];
            for (int i = 0; i < uvCount; i++)
            {
                uvs[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            }
            int indexCount = reader.ReadInt32();
            int[] faces = new int[indexCount];
            for (int i = 0; i < indexCount; i++)
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
