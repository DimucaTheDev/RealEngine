using System.Text.Json.Nodes;
using BulletSharp;
using OpenTK.Mathematics;

namespace RE.Core.World.Components
{
    internal class MeshColliderComponent : ColliderComponent
    {
        public override CollisionShape CreateCollisionShape()
        {
            var meshComponent = Owner.GetComponent<MeshComponent>();
            var modelRenderer = meshComponent.GetModelRenderer();

            if (meshComponent == null! || meshComponent.GetModelRenderer().PhysicsIndices == null)
                throw new InvalidOperationException("Model does not have physics data");

            BulletSharp.Math.Matrix startTransform = BulletSharp.Math.Matrix.Identity;
            startTransform.Origin = new BulletSharp.Math.Vector3(modelRenderer.Position.X, modelRenderer.Position.Y, modelRenderer.Position.Z);
            startTransform.Basis = BulletSharp.Math.Matrix.RotationQuaternion(new BulletSharp.Math.Quaternion(modelRenderer.Rotation.X, modelRenderer.Rotation.Y, modelRenderer.Rotation.Z, modelRenderer.Rotation.W));

            var bulletVertices = ConvertToBulletVectors(modelRenderer.PhysicsVertices!, modelRenderer.Scale);
            var bulletIndices = modelRenderer.PhysicsIndices!.ToArray();

            var meshInterface = new TriangleIndexVertexArray(
                bulletIndices,
                bulletVertices);

            var aabbMin = new BulletSharp.Math.Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var aabbMax = new BulletSharp.Math.Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            foreach (var v in bulletVertices)
            {
                aabbMin.X = Math.Min(aabbMin.X, v.X);
                aabbMin.Y = Math.Min(aabbMin.Y, v.Y);
                aabbMin.Z = Math.Min(aabbMin.Z, v.Z);

                aabbMax.X = Math.Max(aabbMax.X, v.X);
                aabbMax.Y = Math.Max(aabbMax.Y, v.Y);
                aabbMax.Z = Math.Max(aabbMax.Z, v.Z);
            }

            CollisionShape meshShape = new BvhTriangleMeshShape(meshInterface, true);
            return meshShape;
        }
        private static BulletSharp.Math.Vector3[] ConvertToBulletVectors(float[] data, Vector3 scale)
        {
            if (data.Length % 3 != 0)
                throw new ArgumentException("PhysicsVertices should be multiple of 3");

            var result = new BulletSharp.Math.Vector3[data.Length / 3];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = new BulletSharp.Math.Vector3(
                    data[i * 3 + 0] * scale.X,
                    data[i * 3 + 1] * scale.Y,
                    data[i * 3 + 2] * scale.Z
                );
            }
            return result;
        }
        public override JsonNode GetSaveData()
        {
            JsonObject root = new();
            return root;
        }
    }
}
