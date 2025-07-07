using OpenTK.Mathematics;

namespace RE.Rendering;

public interface ICullable
{
    public Vector3 Position { get; set; }
    public Vector3 Scale { get; set; }
    public Quaternion Rotation { get; set; }
    public bool ShouldCull { get; set; }
}