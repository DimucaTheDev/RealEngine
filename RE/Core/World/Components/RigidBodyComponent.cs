using BulletSharp;
using BulletSharp.Math;
using OpenTK.Windowing.Common;
using RE.Core.World.Physics;
using RE.Utils;

namespace RE.Core.World.Components
{
    internal class RigidBodyComponent : Component, IPhysicsComponent
    {
        private RigidBody _rigidBody;
        public float Mass
        {
            get => field;
            set
            {
                if (IsPhysicsObjectInitialized)
                {
                    PhysicsManager.DynamicsWorld.RemoveRigidBody(_rigidBody);

                    float newMass = value;
                    Vector3 localInertia = Vector3.Zero;

                    if (newMass != 0.0f)
                    {
                        _rigidBody.CollisionShape.CalculateLocalInertia(newMass, out localInertia);
                    }
                    _rigidBody.SetMassProps(newMass, localInertia);
                    _rigidBody.LinearVelocity = Vector3.Zero;
                    _rigidBody.AngularVelocity = Vector3.Zero;

                    PhysicsManager.DynamicsWorld.AddRigidBody(_rigidBody);
                }
                field = value;
            }
        }
        public bool IsPhysicsObjectInitialized => _rigidBody != null;

        public RigidBodyComponent() => Mass = 1;
        public RigidBodyComponent(float mass) => Mass = mass;

        public override void Start()
        {
            TryInitializePhysics();
            _rigidBody.Activate();
        }

        public void TryInitializePhysics()
        {
            if (_rigidBody != null!) return;

            var collider = GetComponent<ColliderComponent>();
            if (collider?.IsPhysicsObjectInitialized ?? false)
            {
                _rigidBody = collider.RigidBody;
                Mass = Mass;
            }
            else
            {
                var transform = Owner.Transform;
                var startTransform = Matrix.Identity;

                startTransform.Origin = transform.Position.ToBulletVector3();
                startTransform.Basis =
                    Matrix.RotationQuaternion(transform.Rotation.ToBulletQuaternion());
                Vector3 localInertia = Vector3.Zero;

                if (Mass > 0f)
                    collider?.CollisionShape?.CalculateLocalInertia(Mass, out localInertia);

                var motionState = new DefaultMotionState(startTransform);
                var rbInfo = new RigidBodyConstructionInfo(Mass, motionState, collider?.CollisionShape ?? new BoxShape(0.5f), localInertia);
                _rigidBody = new RigidBody(rbInfo) { UserObject = this };

                PhysicsManager.DynamicsWorld.AddRigidBody(_rigidBody);
            }
        }

        public override void Update(FrameEventArgs args)
        {
            if (_rigidBody == null || _rigidBody.MotionState == null || Mass == 0f)
                return;

            _rigidBody.GetWorldTransform(out var bulletTransform);
            Owner.Transform.Position = new OpenTK.Mathematics.Vector3(bulletTransform.Origin.X, bulletTransform.Origin.Y, bulletTransform.Origin.Z);

            var bulletRotation = Quaternion.RotationMatrix(bulletTransform.Basis);
            Owner.Transform.Rotation = new OpenTK.Mathematics.Quaternion(bulletRotation.X, bulletRotation.Y, bulletRotation.Z, bulletRotation.W);
        }

        public override void OnDestroy()
        {
            if (_rigidBody != null!)
            {
                PhysicsManager.DynamicsWorld.RemoveRigidBody(_rigidBody);
                _rigidBody.Dispose();
                _rigidBody = null!;
            }

            base.OnDestroy();
        }

        public RigidBody GetRigidBody() => _rigidBody;
    }
}
