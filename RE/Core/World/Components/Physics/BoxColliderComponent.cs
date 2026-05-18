using System.Text.Json.Nodes;
using BulletSharp;
using BulletSharp.Math;
using RE.Core.Scripting.Attributes;
using RE.Utils;

namespace RE.Core.World.Components.Physics
{
    [ComponentInfo("Physics/Collision")]
    internal class BoxColliderComponent : ColliderComponent
    {
        public override CollisionShape CreateCollisionShape()
        {
            var mesh = Owner.GetComponent<MeshComponent>();

            if (mesh is null)
                return new BoxShape(0.5f);

            var size = mesh.ModelRenderer.Model.Mesh.MaxBounds - mesh.ModelRenderer.Model.Mesh.MinBounds;

            var halfExtents = size * 0.5f * Owner.Transform.Scale;

            return new BoxShape(halfExtents.ToBulletVector3());
        }

        public override JsonNode GetSaveData() => new JsonObject();
    }
}