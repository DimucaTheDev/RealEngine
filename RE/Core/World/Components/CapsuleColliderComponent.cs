using BulletSharp;

namespace RE.Core.World.Components
{
    internal class CapsuleColliderComponent : ColliderComponent
    {
        public override CollisionShape CreateCollisionShape()
        {
            var scale = Owner.Transform.Scale;
            CollisionShape shape = new CapsuleShape(MathF.Max(scale.X, scale.Z), scale.Y);
            return shape;
        }
    }
}
