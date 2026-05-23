using OpenTK.Mathematics;
using RE.Core.Assets;

namespace RE.Rendering;

public class ModelMesh : ICloneable
{
    public uint Vao;
    public uint Vbo;
    public uint Ebo;
    public int IndexCount;
    public Vector3 MinBounds;
    public Vector3 MaxBounds;
    public List<Vector3> Vertices;
    public List<uint> Indices;

    /// <inheritdoc />
    public object Clone()
    {
        return new ModelMesh()
        {
            Vao = Vao,
            Vbo = Vbo,
            Ebo = Ebo,
            IndexCount = IndexCount,
            MinBounds = MinBounds,
            MaxBounds = MaxBounds,
            Vertices = Vertices,
            Indices = Indices
        };
    }
}