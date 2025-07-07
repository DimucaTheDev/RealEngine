using BulletSharp;

namespace RE.Core.World.Components
{
    internal class SphereColliderComponent : ColliderComponent
    {
        protected override CollisionShape CreateCollisionShape()
        {
            var scale = Owner.Transform.Scale;
            var max = MathF.Max(scale.X, MathF.Max(scale.Y, scale.Z));
            CollisionShape sphereShape = new SphereShape(max);
            return sphereShape;
        }
    }
}
