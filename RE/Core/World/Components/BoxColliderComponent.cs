using BulletSharp;

namespace RE.Core.World.Components
{
    internal class BoxColliderComponent : ColliderComponent
    {
        public override CollisionShape CreateCollisionShape()
        {
            BulletSharp.Math.Vector3 halfExtents = new BulletSharp.Math.Vector3(
                Owner.Transform.Scale.X * 1,// 0.5f,
                Owner.Transform.Scale.Y * 1,// 0.5f,
                Owner.Transform.Scale.Z * 1 // 0.5f
            );
            CollisionShape boxShape = new BoxShape(halfExtents);

            return boxShape;
        }
    }
}