using System.Text.Json.Nodes;
using BulletSharp;
using RE.Core.Scripting;
using RE.Utils;

namespace RE.Core.World.Components.Physics
{
    [ComponentInfo("Physics/Collision")]
    internal class BoxColliderComponent : ColliderComponent
    {
        public override CollisionShape CreateCollisionShape()
        {
            BulletSharp.Math.Vector3 halfExtents = new BulletSharp.Math.Vector3(
                Owner.Transform.Scale.X * 1,// 0.5f,
                Owner.Transform.Scale.Y * 1,// 0.5f,
                Owner.Transform.Scale.Z * 1 // 0.5f
            );
            CollisionShape boxShape = new BoxShape(halfExtents * Multiplier.ToBulletVector3());
            return boxShape;
        }
        public override JsonNode GetSaveData()
        {
            JsonObject root = new() { { nameof(Multiplier), new JsonArray() { Multiplier.X, Multiplier.Y, Multiplier.Z } } };
            return root;
        }
    }
}