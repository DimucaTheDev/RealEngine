using OpenTK.Mathematics;

namespace RE.Core.World
{
    public class Transform : ICloneable
    {
        public Vector3 Position { get; set; }
        public Vector3 Scale { get; set; }
        public Quaternion Rotation { get; set; }

        public object Clone()
        {
            return new Transform() { Position = Position, Rotation = Rotation, Scale = Scale };
        }
    }
}
