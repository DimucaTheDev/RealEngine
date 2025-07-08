using OpenTK.Mathematics;
using RE.Core.World.Components;
using RE.Utils;

namespace RE.Core.World
{
    internal class GameObject
    {
        public GameObject()
        {
            Components = new ComponentList(this);
            Transform = new Transform()
            {
                Position = Vector3.Zero,
                Rotation = Quaternion.Identity,
                Scale = Vector3.One
            };
        }

        public ComponentList Components { get; }
        public Transform Transform { get; set; }
        public string Name { get; set; }

        public void SetPosition(Vector3 position)
        {
            Transform.Position = position;
            var rigidBodyComponent = GetComponent<RigidBodyComponent>();
            if (rigidBodyComponent != null!)
            {
                var rigidBody = rigidBodyComponent.GetRigidBody();
                var transform = rigidBody.WorldTransform;
                transform.Origin = position.ToBulletVector3();
                rigidBody.WorldTransform = transform;
                rigidBody.MotionState?.SetWorldTransform(ref transform);
            }
        }

        public void SetRotation(Quaternion q)
        {
            Transform.Rotation = q;
            var rigidBodyComponent = GetComponent<RigidBodyComponent>();
            if (rigidBodyComponent != null!)
            {
                var rigidBody = rigidBodyComponent.GetRigidBody();
                var transform = rigidBody.WorldTransform;
                transform.Basis = BulletSharp.Math.Matrix.RotationQuaternion(
                    q.ToBulletQuaternion()
                );
                rigidBody.WorldTransform = transform;
                rigidBody.MotionState?.SetWorldTransform(ref transform);

            }
        }

        public T GetComponent<T>() where T : Component
        {
            return (T)Components.FirstOrDefault(s => s is T)!;
        }
    }
}
