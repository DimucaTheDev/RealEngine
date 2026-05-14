using System.Text.Json.Nodes;
using BulletSharp;
using BulletSharp.Math;
using JetBrains.Annotations;
using OpenTK.Windowing.Common;
using RE.Core.Scripting.Attributes;
using RE.Core.World.Physics;
using RE.Editor;
using RE.Utils;
using Serilog;

namespace RE.Core.World.Components.Physics
{
    [ComponentInfo("Physics", Description = "Represents a dynamic physics body with mass and velocity")]
    internal class RigidBodyComponent(float mass) : Component
    {
        [UsedImplicitly]
        private RigidBodyComponent() : this(0)
        {
        }

        public RigidBody RigidBody { get; private set; }

        private CollisionShape _shape;
        private bool _dirty = true;
        private Vector3 _previousPosition;
        private Vector3 _currentPosition;
        private Quaternion _previousRotation;
        private Quaternion _currentRotation;

        [EditorProperty]
        public float Mass
        {
            get;
            set
            {
                field = value;
                RebuildPhysics();
            }
        } = mass;

        [EditorProperty]
        public float Friction
        {
            get;
            set
            {
                field = value;
                RigidBody.Friction = value;
            }
        } = 1;

        public override void Start()
        {
            RebuildPhysics();
            RigidBody?.Activate();
        }

        public override void PhysicsSync()
        {
            RigidBody.GetWorldTransform(out var t);

            _previousPosition = _currentPosition;
            _previousRotation = _currentRotation;

            _currentPosition = t.Origin;
            _currentRotation = Quaternion.RotationMatrix(t.Basis);
        }

        public override void Update(FrameEventArgs args)
        {
            if (RigidBody == null)
                return;

            if (_dirty)
                RebuildPhysics();

            if ((RigidBody.CollisionFlags & CollisionFlags.KinematicObject) != 0)
                return;


            float alpha = PhysicsManager.Alpha;

            Owner.Transform.Position =
                Vector3.Lerp(
                    _previousPosition,
                    _currentPosition,
                    alpha).ToOpenTkVector3();

            Owner.Transform.Rotation =
                Quaternion.Slerp(
                    _previousRotation,
                    _currentRotation,
                    alpha).ToOpenTkQuaternion();
        }

        public void MarkDirty()
        {
            _dirty = true;
        }

        private CollisionShape BuildShape()
        {
            var colliders = Owner.GetComponents<ColliderComponent>();

            if (colliders.Count == 0)
                return new EmptyShape();

            if (colliders.Count == 1)
                return colliders[0].CreateCollisionShape();

            var compound = new CompoundShape();

            foreach (var c in colliders)
            {
                var shape = c.CreateCollisionShape();
                compound.AddChildShape(Matrix.Identity, shape);
            }

            return compound;
        }

        private void RebuildPhysics()
        {
            Log.Information("Rebuild {obj}. {f}", Owner, RigidBody == null);
            if (RigidBody != null)
            {
                PhysicsManager.DynamicsWorld.RemoveRigidBody(RigidBody);
                RigidBody.Dispose();
                RigidBody = null;
            }

            _shape?.Dispose();
            _shape = BuildShape();

            var transform = Owner.Transform;

            var start = Matrix.Identity;
            start.Origin = transform.Position.ToBulletVector3();
            start.Basis = Matrix.RotationQuaternion(transform.Rotation.ToBulletQuaternion());

            Vector3 inertia = Vector3.Zero;
            if (Mass != 0)
                _shape.CalculateLocalInertia(Mass, out inertia);

            var motion = new DefaultMotionState(start);

            var info = new RigidBodyConstructionInfo(Mass, motion, _shape, inertia);

            RigidBody = new RigidBody(info)
            {
                UserObject = this,
                Friction = Friction,
            };

            PhysicsManager.DynamicsWorld.AddRigidBody(RigidBody);

            _dirty = false;
        }

        public override void OnDestroy()
        {
            if (RigidBody != null)
            {
                PhysicsManager.DynamicsWorld.RemoveRigidBody(RigidBody);
                RigidBody.Dispose();
                RigidBody = null;
            }

            _shape?.Dispose();

            base.OnDestroy();
        }

        public override JsonNode GetSaveData()
        {
            return new JsonObject
            {
                { nameof(Mass), Mass }
            };
        }
    }
}