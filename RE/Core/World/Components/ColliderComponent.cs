using BulletSharp;
using RE.Core.World.Physics;
using RE.Utils;

namespace RE.Core.World.Components
{
    internal abstract class ColliderComponent : Component, IPhysicsComponent
    {
        protected CollisionShape _collisionShape;
        protected RigidBody _rigidBody;

        public CollisionShape CollisionShape => _collisionShape;
        public RigidBody RigidBody => _rigidBody;
        public bool IsPhysicsObjectInitialized => _collisionShape != null;

        public override void Start()
        {
            _collisionShape = CreateCollisionShape();
            TryInitializePhysics();
        }

        public void TryInitializePhysics()
        {
            var rigid = GetComponent<RigidBodyComponent>();
            if (rigid?.IsPhysicsObjectInitialized ?? false)
            {
                _rigidBody = rigid.GetRigidBody();
                _rigidBody.CollisionShape = _collisionShape;
                rigid.Mass = rigid.Mass;
            }
            else
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
        }

        protected abstract CollisionShape CreateCollisionShape();

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