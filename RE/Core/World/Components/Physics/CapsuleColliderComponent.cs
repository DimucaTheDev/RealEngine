using System.Text.Json.Nodes;
using BulletSharp;
using RE.Core.Scripting.Attributes;

namespace RE.Core.World.Components.Physics
{
    [ComponentInfo("Physics/Collision")]
    internal class CapsuleColliderComponent : ColliderComponent
    {
        public override CollisionShape CreateCollisionShape()
        {
            var scale = Owner.Transform.Scale;
            CollisionShape shape = new CapsuleShape(MathF.Max(scale.X, scale.Z), scale.Y);
            return shape;
        }
        public override JsonNode GetSaveData()
        {
            JsonObject root = new() { { nameof(Multiplier), new JsonArray { Multiplier } } };
            return root;
        }
    }
}
