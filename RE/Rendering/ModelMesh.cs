using OpenTK.Mathematics;
using RE.Core.Assets;

namespace RE.Rendering;

public class ModelMesh
{
    public uint Vao;
    public uint Vbo;
    public uint Ebo;
    public int IndexCount;
    public Vector3 MinBounds;
    public Vector3 MaxBounds;
    public List<Vector3> Vertices;
    public List<uint> Indices;
}