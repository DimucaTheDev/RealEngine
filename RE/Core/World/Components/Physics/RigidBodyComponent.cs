using System.Text.Json.Nodes;
using BulletSharp;
using BulletSharp.Math;
using OpenTK.Windowing.Common;
using RE.Core.Scripting;
using RE.Core.World.Physics;
using RE.Debug;
using RE.Rendering.Renderables;
using RE.Rendering.Text;
using RE.Utils;

namespace RE.Core.World.Components.Physics
{
    [ComponentInfo("Physics", Description = "Represents a dynamic physics body with mass and velocity")]
    internal class RigidBodyComponent(float mass) : Component, IPhysicsComponent, IDebugRenderer
    {
        public RigidBody RigidBody = null!;

        private FloatingText? _debugText = null!;

        [EditorProperty]
        public float Mass
        {
            get;
            set
            {
                if (IsPhysicsObjectInitialized)
                {


                    PhysicsManager.DynamicsWorld.RemoveRigidBody(RigidBody);

                    float newMass = value;
                    Vector3 localInertia = Vector3.Zero;

                    if (newMass != 0.0f)
                    {
                        RigidBody.CollisionShape.CalculateLocalInertia(newMass, out localInertia);
                    }

                    RigidBody.SetMassProps(newMass, localInertia);
                    //RigidBody.LinearVelocity = Vector3.Zero;
                    //RigidBody.AngularVelocity = Vector3.Zero;

                    PhysicsManager.DynamicsWorld.AddRigidBody(RigidBody);
                }

                field = value;
            }
        } = mass;

        public bool IsPhysicsObjectInitialized => RigidBody != null;

        public RigidBodyComponent() : this(1) { }


        public override void Start()
        {
            if (IsPhysicsObjectInitialized)
                return;
            TryInitializePhysics();
            RigidBody.Activate();
            _debugText = new("", OpenTK.Mathematics.Vector3.Zero, (FreeTypeFont)Fonts.Consolas);
        }

        public void TryInitializePhysics()
        {
            if (RigidBody != null!)
                return;

            var collider = GetComponent<ColliderComponent>();
            if (collider?.IsPhysicsObjectInitialized ?? false)
            {
                RigidBody = collider.RigidBody;
                Mass = Mass;
            }
            else if (!RigidBody?.IsInWorld ?? true)
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
                var rbInfo = new RigidBodyConstructionInfo(Mass, motionState,
                    collider?.CollisionShape ?? new BoxShape(0.5f), localInertia);
                RigidBody = new RigidBody(rbInfo) { UserObject = this };

                PhysicsManager.DynamicsWorld.AddRigidBody(RigidBody);
            }
        }

        public override void Update(FrameEventArgs args)
        {
            if (_debugText != null)
            {
                _debugText.Position = Owner.Transform.Position + (0, Owner.Transform.Scale.Y + 1, 0);
                _debugText.Text = $"Mass: {Mass}\n" +
                                  $"Velocity: {RigidBody?.LinearVelocity.Length ?? 0f:0.00}\n" +
                                  $"Angular Velocity: {RigidBody?.AngularVelocity.Length ?? 0f:0.00}";
            }

            if (RigidBody == null || RigidBody.MotionState == null || Mass == 0f)
                return;

            RigidBody.GetWorldTransform(out var bulletTransform);
            Owner.Transform.Position = new OpenTK.Mathematics.Vector3(bulletTransform.Origin.X,
                bulletTransform.Origin.Y, bulletTransform.Origin.Z);

            var bulletRotation = Quaternion.RotationMatrix(bulletTransform.Basis);
            Owner.Transform.Rotation = new OpenTK.Mathematics.Quaternion(bulletRotation.X, bulletRotation.Y,
                bulletRotation.Z, bulletRotation.W);
        }

        public override void OnDestroy()
        {
            if (RigidBody != null!)
            {
                PhysicsManager.DynamicsWorld.RemoveRigidBody(RigidBody);
                RigidBody.Dispose();
                RigidBody = null!;
            }
            base.OnDestroy();
        }
        public override JsonNode GetSaveData()
        {
            JsonObject root = new() { { nameof(Mass), Mass } };
            return root;
        }

        public void DebugRender(FrameEventArgs args)
        {
            _debugText?.Render(args);
        }
    }
}