using System.Text.Json.Nodes;
using BulletSharp;
using RE.Core.Scripting.Attributes;

namespace RE.Core.World.Components.Physics
{
    [ComponentInfo("Physics/Collision")]
    internal class SphereColliderComponent : ColliderComponent
    {
        public override CollisionShape CreateCollisionShape()
        {
            var scale = Owner.Transform.Scale;
            var max = MathF.Max(scale.X, MathF.Max(scale.Y, scale.Z));
            CollisionShape sphereShape = new SphereShape(max);
            return sphereShape;
        }

        public override JsonObject GetSaveData() => new JsonObject();
    }
}
