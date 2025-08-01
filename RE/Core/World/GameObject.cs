using OpenTK.Mathematics;
using RE.Core.World.Components;
using RE.Utils;

namespace RE.Core.World
{
    public class GameObject
    {
        private static int _next = 0;

        public GameObject() : this(null) { }
        public GameObject(GameObject? parent)
        {
            Components = new ComponentList(this);
            Parent = parent;
            Transform = new Transform()
            {
                Position = Vector3.Zero,
                Rotation = Quaternion.Identity,
                Scale = Vector3.One
            };
            Id = _next++;
        }

        public ComponentList Components { get; }
        public Transform Transform { get; set; }
        public GameObject? Parent { get; set; }
        public string? Name { get; set; }
        public int Id { get; private set; }
        public string? Tag { get; set; }
        public bool DoNotSave { get; set; } = false;
        public bool DoNotShowInEditor { get; set; } = false;

        public Scene Scene { get; set; }
        public IReadOnlyList<GameObject> Children => Scene.GameObjects.Where(s => s.Parent == this).ToList().AsReadOnly();

        public void SetPosition() => SetPosition(Transform.Position);
        public void SetPosition(Vector3 position)
        {
            Transform.Position = position;

            var rigidBody = GetComponent<RigidBodyComponent>()?.RigidBody
                            ?? GetComponent<ColliderComponent>()?.RigidBody;

            if (rigidBody == null)
                return;

            var transform = rigidBody.WorldTransform;
            transform.Origin = position.ToBulletVector3();
            rigidBody.WorldTransform = transform;
            rigidBody.MotionState?.SetWorldTransform(ref transform);
        }


        public void SetRotation() => SetRotation(Transform.Rotation);
        public void SetRotation(Quaternion q)
        {
            Transform.Rotation = q;
            var rigidBodyComponent = GetComponent<RigidBodyComponent>();
            if (rigidBodyComponent != null!)
            {
                var rigidBody = rigidBodyComponent.RigidBody;
                var transform = rigidBody.WorldTransform;
                transform.Basis = BulletSharp.Math.Matrix.RotationQuaternion(
                    q.ToBulletQuaternion()
                );
                rigidBody.WorldTransform = transform;
                rigidBody.MotionState?.SetWorldTransform(ref transform);

            }
        }

        public T? GetComponent<T>() where T : Component
        {
            return Components.OfType<T>().FirstOrDefault();
        }
    }
}
