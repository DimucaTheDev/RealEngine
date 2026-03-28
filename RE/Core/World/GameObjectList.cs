using System.Collections;
using BulletSharp;
using OpenTK.Mathematics;
using RE.Core.World.Components;
using RE.Editor.Panels.Viewport;
using Serilog;

namespace RE.Core.World
{
    /// <summary>
    /// Represents a collection of <see cref="GameObject"/> within a <see cref="Scene"/>.
    /// </summary>
    public class GameObjectList(Scene scene) : IEnumerable<GameObject>
    {
        internal readonly List<GameObject> _objects = new();

        public GameObject FindByTag(string tag) =>
            _objects.First(g => g.Tag!.Equals(tag, StringComparison.InvariantCultureIgnoreCase));
        public GameObject FindByName(string name) =>
            _objects.First(g => g.Name!.Equals(name, StringComparison.InvariantCultureIgnoreCase));

        public IEnumerator<GameObject> GetEnumerator() => _objects.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(GameObject g, bool doNotCallStart = false)
        {
            if (_objects.Contains(g))
            {
                Log.Error("Tried to add an object that is already in the scene: {Name}", g.Name);
                return;
            }

            SceneManager._objectsToAdd.Add(g);
            g.Scene = scene;

            if (!doNotCallStart)
            {
                foreach (var component in g.Components)
                {
                    component.Start();
                }
            }

            CreateVpObject(g);
        }

        public void Remove(GameObject g)
        {
            foreach (var child in g.Children.ToList())
            {
                Remove(child);
            }
            SceneManager._objectsToRemove.Add(g);
        }

        private void CreateVpObject(GameObject g)
        {
            CollisionShape shape;
            g.ViewportObject = new CollisionObject();

            if (g.GetComponent<MeshComponent>() is { } meshComponent &&
                meshComponent.GetModelRenderer() is { PhysicsIndices: not null } modelRenderer)
            {
                var bulletVertices = ConvertToBulletVectors(modelRenderer.PhysicsVertices!, modelRenderer.Scale);
                var bulletIndices = modelRenderer.PhysicsIndices!.ToArray();

                var meshInterface = new TriangleIndexVertexArray(
                    bulletIndices,
                    bulletVertices);

                shape = new BvhTriangleMeshShape(meshInterface, true);
            }
            else
                shape = g.Components.FirstOrDefault(s => s.GetObjectSelectionShape() is not EmptyShape)
                    ?.GetObjectSelectionShape() ?? new EmptyShape();

            g.ViewportObject.CollisionShape = shape;
            g.ViewportObject.UserObject = g;


            var pos = g.Transform.Position;
            var rot = g.Transform.Rotation;

            var position = new BulletSharp.Math.Vector3(pos.X, pos.Y, pos.Z);
            var rotation = new BulletSharp.Math.Quaternion(rot.X, rot.Y, rot.Z, rot.W);

            g.ViewportObject.WorldTransform =
                BulletSharp.Math.Matrix.RotationQuaternion(rotation) * BulletSharp.Math.Matrix.Translation(position);

            var scale = g.Transform.Scale;
            g.ViewportObject.CollisionShape.LocalScaling =
                new BulletSharp.Math.Vector3(scale.X, scale.Y, scale.Z);

            ViewportPanel.CollisionWorld.AddCollisionObject(g.ViewportObject);
        }

        private static BulletSharp.Math.Vector3[] ConvertToBulletVectors(float[] data, Vector3 scale)
        {
            if (data.Length % 3 != 0)
                throw new ArgumentException("PhysicsVertices must be multiple of 3");

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
    }
}
