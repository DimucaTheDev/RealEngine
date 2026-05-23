using System.Text.Json.Nodes;
using BulletSharp;
using BulletSharp.Math;
using JetBrains.Annotations;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using RE.Core.Assets;
using RE.Core.Scripting.Attributes;
using RE.Core.World.Physics;
using RE.Editor;
using RE.Rendering.Renderables;
using RE.Rendering.Texturing;
using RE.Utils;
using Serilog;

namespace RE.Core.World.Components.Physics
{
    [ComponentInfo("Physics", Description = "Represents a dynamic physics body with mass and velocity")]
    internal class RigidBodyComponent(float mass) : Component, IEditorRender, IEditorUpdate
    {
        [UsedImplicitly]
        private RigidBodyComponent() : this(0)
        {
        }

        private CollisionShape _shape;
        private bool _dirty = true;
        private Vector3 _previousPosition;
        private Vector3 _currentPosition;
        private Quaternion _previousRotation;
        private Quaternion _currentRotation;
        private ModelRenderer _triggerBoxRenderer;

        public override bool IsOpaque => false;

        public RigidBody RigidBody { get; private set; }

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
            get => RigidBody != null! ? (field = RigidBody.Friction) : field;
            set => RigidBody.Friction = field = value;
        } = 1;

        [EditorProperty]
        public bool IsTrigger
        {
            get => RigidBody is { IsInWorld: true }
                ? (field =
                    (RigidBody.CollisionFlags & (CollisionFlags.NoContactResponse | CollisionFlags.StaticObject)) != 0)
                : field;
            set
            {
                field = value;
                
                if(RigidBody == null!) return;
                
                if (value)
                {
                    RigidBody.CollisionFlags |= CollisionFlags.NoContactResponse | CollisionFlags.StaticObject;
                    //Mass = 0;
                }
                else
                    RigidBody.CollisionFlags &= ~(CollisionFlags.NoContactResponse | CollisionFlags.StaticObject);
            }
        } = false;

        [EditorProperty]
        public bool IsKinematic
        {
            get => RigidBody is { IsInWorld: true }
                ? (field =
                    (RigidBody.CollisionFlags & (CollisionFlags.KinematicObject)) != 0)
                : field;
            set
            {
                field = value;
                
                if(RigidBody == null!) return;
                
                if (value)
                {
                    RigidBody.CollisionFlags |= CollisionFlags.KinematicObject;
                    //Mass = 0;
                }
                else
                    RigidBody.CollisionFlags &= ~(CollisionFlags.KinematicObject);
            }
        } = false;

        public override void Start()
        {
            RebuildPhysics();
            RigidBody?.Activate();

            _triggerBoxRenderer = ModelRenderer.Create("assets/testing/trigger.fbx");
            _triggerBoxRenderer.IgnoreLight = true;
            _triggerBoxRenderer.Model.Material.ShaderProgram = new ShaderProgram();
            _triggerBoxRenderer.Model.Material.ShaderProgram.AttachShader("assets/shaders/trigger.vert");
            _triggerBoxRenderer.Model.Material.ShaderProgram.AttachShader("assets/shaders/trigger.frag");
            _triggerBoxRenderer.Model.Material.Texture = new StaticTexture("assets/testing/trigger.png");
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

            if (!RigidBody.IsInWorld)
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

        internal void RebuildPhysics()
        {
            if (!IsEnabled)
            {
                Log.Warning("Rebuilding physics in disabled component on {GameObject}", Owner);
                return;
            }

            Log.Verbose("Rebuild {obj}. {f}", Owner, RigidBody == null!);
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
                CollisionFlags = (IsTrigger ? (CollisionFlags.NoContactResponse | CollisionFlags.StaticObject) : 0)
                                 | (IsKinematic ? CollisionFlags.KinematicObject : 0)
            };

            PhysicsManager.DynamicsWorld.AddRigidBody(RigidBody);

            _dirty = false;
        }

        /// <inheritdoc />
        public override CollisionShape GetObjectSelectionShape() => BuildShape();

        protected override void Enabled()
        {
            RigidBody.Enable();
            RigidBody.Activate();
        }

        protected override void Disabled() => RigidBody.Disable();

        public override void Destroy()
        {
            if (RigidBody != null)
            {
                PhysicsManager.DynamicsWorld.RemoveRigidBody(RigidBody);
                RigidBody.Dispose();
                RigidBody = null;
            }

            _shape?.Dispose();

            base.Destroy();
        }

        public override JsonNode GetSaveData()
        {
            return new JsonObject
            {
                { nameof(Mass), Mass }
            };
        }

        public void EditorRender(FrameEventArgs args)
        {
            if (IsTrigger)
            {
                GL.Enable(EnableCap.CullFace);
                GL.CullFace(TriangleFace.Back);
                _triggerBoxRenderer.Render(args);
                GL.Disable(EnableCap.CullFace);
            }
        }

        /// <inheritdoc />
        public void EditorUpdate(FrameEventArgs args)
        {
            if (IsTrigger)
            {
                _triggerBoxRenderer.Position = Owner.Transform.Position;
                _triggerBoxRenderer.Rotation = Owner.Transform.Rotation;
                _triggerBoxRenderer.Scale = Owner.Transform.Scale;
            }
        }
    }
}