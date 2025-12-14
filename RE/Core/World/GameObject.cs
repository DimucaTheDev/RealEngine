using BulletSharp;
using OpenTK.Mathematics;
using RE.Core.World.Components;
using RE.Core.World.Components.Physics;
using RE.Utils;

namespace RE.Core.World
{
    /// <summary>
    /// Represents an object in the game world with components, transform, and hierarchy.
    /// </summary>
    public class GameObject
    {
        // this is used in the viewport for selection. todo: make public
        internal CollisionObject ViewportObject;

        private static int _next = 0;
         
        /// <summary>
        /// Initializes a new instance of <see cref="GameObject"/> with no parent.
        /// </summary>
        public GameObject() : this(null) { }

        /// <summary>
        /// Initializes a new instance of <see cref="GameObject"/> with the specified parent.
        /// </summary>
        /// <param name="parent">The parent <see cref="GameObject"/>. Can be null.</param>
        public GameObject(GameObject? parent)
        {
            Components = new ComponentList(this);
            Parent = parent;
            Transform = new Transform
            {
                Position = Vector3.Zero,
                Rotation = Quaternion.Identity,
                Scale = Vector3.One
            };
            Id = _next++;
        }

        public ComponentList Components { get; }

        /// <summary>
        /// Gets or sets the transform containing position, rotation, and scale.
        /// </summary>
        public Transform Transform { get; set; }

        public GameObject? Parent { get; set; }

        public string? Name { get; set; }

        public int Id { get; private set; }

        /// <summary>
        /// Tags are used for finding objects by tag (see <see cref="GameObjectList.FindByTag"/>).
        /// </summary>
        /// <remarks>
        /// Tag must be unique per scene.
        /// </remarks>
        public string? Tag { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this object should be excluded from saving (see <see cref="SceneManager.SaveSceneToFile"/>).
        /// </summary>
        public bool DoNotSave { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether this object should be hidden in the editor.
        /// </summary>
        public bool DoNotShowInEditor { get; set; } = false;

        /// <summary>
        /// Gets or sets the <see cref="Scene"/> to which this object belongs.
        /// </summary>
        public Scene Scene { get; set; } = null!;

        /// <summary>
        /// Gets a read-only list of child <see cref="GameObject"/> instances under this object.
        /// </summary>
        public IReadOnlyList<GameObject> Children => Scene?.GameObjects.Where(s => s.Parent == this).ToList().AsReadOnly() ?? [];

        /// <summary>
        /// Sets the position of the game object and updates the associated physics rigid body if present.
        /// </summary>
        /// <param name="position">The new position to set.</param>
        public void SetPosition(Vector3 position)
        {
            //todo: move all children [ childPos + (newPos - oldPos) ]
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

        /// <summary>
        /// Sets the rotation of the game object and updates the associated physics rigid body if present.
        /// </summary>
        /// <param name="q">The new rotation quaternion to set.</param>
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

        /// <summary>
        /// Gets the first component of type <typeparamref name="T"/> attached to this game object.
        /// </summary>
        /// <typeparam name="T">The type of component to retrieve.</typeparam>
        /// <returns>The first component of type <typeparamref name="T"/> if found; otherwise, <c>null</c>.</returns>
        public T? GetComponent<T>() where T : Component
        {
            return Components.OfType<T>().FirstOrDefault();
        }
    }
}
