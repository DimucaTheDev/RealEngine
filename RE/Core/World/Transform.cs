using OpenTK.Mathematics;

namespace RE.Core.World
{
    /// <summary>
    /// Represents the position, rotation, and scale of a <see cref="GameObject"/>.
    /// </summary>
    public class Transform : ICloneable
    {
        /// <summary>
        /// Object position in world space.
        /// </summary>
        public Vector3 Position { get; set; }
        /// <summary>
        /// Object scale. Default is <see cref="Vector3.One"/>.
        /// </summary>
        public Vector3 Scale { get; set; }
        /// <summary>
        /// Object rotation as quaternion.
        /// </summary>
        public Quaternion Rotation { get; set; }

        public object Clone()
        {
            return new Transform() { Position = Position, Rotation = Rotation, Scale = Scale };
        }
    }
}
