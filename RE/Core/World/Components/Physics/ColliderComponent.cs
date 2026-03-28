using BulletSharp;
using OpenTK.Mathematics;
using RE.Core.Scripting.Attributes;
using RE.Core.World.Physics;
using RE.Utils;

namespace RE.Core.World.Components.Physics
{
    internal abstract class ColliderComponent : Component, IPhysicsComponent
    {
#pragma warning disable 8618
        private CollisionShape _collisionShape;
        private RigidBody _rigidBody;
#pragma warning restore
        public CollisionShape CollisionShape => _collisionShape;
        public RigidBody RigidBody => _rigidBody;
        public bool IsPhysicsObjectInitialized => _collisionShape != null!;

        [EditorProperty]
        public Vector3 Multiplier
        {
            get;
            set
            {
                field = value;
                TryInitializePhysics();
            }
        } = Vector3.One;

        public override void Start()
        {
            if (IsPhysicsObjectInitialized)
                return;
            TryInitializePhysics();
        }

        public void TryInitializePhysics()
        {
            _collisionShape = CreateCollisionShape();

            var rigid = GetComponent<RigidBodyComponent>();
            if (rigid?.IsPhysicsObjectInitialized ?? false)
            {
                _rigidBody = rigid.RigidBody;
                _rigidBody.CollisionShape = _collisionShape;
                rigid.Mass = rigid.Mass;
            }
            else if (!_rigidBody?.IsInWorld ?? true)
            {

                var transform = Owner.Transform;
                var startTransform = BulletSharp.Math.Matrix.Identity;
                startTransform.Origin = transform.Position.ToBulletVector3();
                startTransform.Basis =
                    BulletSharp.Math.Matrix.RotationQuaternion(transform.Rotation.ToBulletQuaternion());

                var motionState = new DefaultMotionState(startTransform);
                var rbInfo = new RigidBodyConstructionInfo(0f, motionState, _collisionShape, BulletSharp.Math.Vector3.Zero);
                _rigidBody = new RigidBody(rbInfo)
                {
                    UserObject = this
                };
                PhysicsManager.DynamicsWorld.AddRigidBody(_rigidBody);
            } 
            _rigidBody?.Friction = 1;
        }

        public abstract CollisionShape CreateCollisionShape();

        public override void OnDestroy()
        {
            if (_rigidBody != null!)
            {
                PhysicsManager.DynamicsWorld.RemoveRigidBody(_rigidBody);
                _rigidBody.Dispose();
                _collisionShape = null!;
                _rigidBody = null!;
            }

            base.OnDestroy();
        } 
    }
}