using BulletSharp;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core.Scripting;
using RE.Core.Scripting.Attributes;
using RE.Core.World.Physics;
using RE.Debug;
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

        public override void Render(FrameEventArgs args)
        {
            if (_rigidBody is { IsInWorld: true } && Variables.GetVariable("renderColliderBorders") is true)
                DrawRigidBodyBounds(_rigidBody, LineRenderer.Main!);
        }

        void DrawRigidBodyBounds(RigidBody body, LineRenderer lineRenderer)
        {
            // Check if the shape is a BoxShape, as this method is specific to drawing boxes
            if (body.CollisionShape is BoxShape boxShape)
            {
                // Get the half-extents of the box.
                // Use HalfExtentsWithMargin if you want to include collision margin,
                // otherwise use boxShape.HalfExtents for the exact geometric size.
                BulletSharp.Math.Vector3 halfExtents = boxShape.HalfExtentsWithoutMargin; // Changed to HalfExtents for exact mesh match

                // Define the 8 corners of the box in its LOCAL space (centered at origin)
                // These are relative to the center of the BoxShape
                Vector3[] localCorners = new Vector3[8];
                localCorners[0] = new Vector3(-halfExtents.X, -halfExtents.Y, -halfExtents.Z);
                localCorners[1] = new Vector3(halfExtents.X, -halfExtents.Y, -halfExtents.Z);
                localCorners[2] = new Vector3(halfExtents.X, halfExtents.Y, -halfExtents.Z);
                localCorners[3] = new Vector3(-halfExtents.X, halfExtents.Y, -halfExtents.Z);
                localCorners[4] = new Vector3(-halfExtents.X, -halfExtents.Y, halfExtents.Z);
                localCorners[5] = new Vector3(halfExtents.X, -halfExtents.Y, halfExtents.Z);
                localCorners[6] = new Vector3(halfExtents.X, halfExtents.Y, halfExtents.Z);
                localCorners[7] = new Vector3(-halfExtents.X, halfExtents.Y, halfExtents.Z);

                // Get the current world transform of the rigid body (position and rotation)
                BulletSharp.Math.Matrix worldTransform = body.WorldTransform;

                // Transform the local corners to world space using the body's transform
                Vector3[] worldCorners = new Vector3[8];
                for (int i = 0; i < 8; i++)
                {
                    // Convert OpenTK Vector3 to BulletSharp Vector3 for transformation
                    BulletSharp.Math.Vector3 bulletLocalCorner = new(localCorners[i].X, localCorners[i].Y, localCorners[i].Z);
                    // Transform by the world matrix. Vector3.TransformCoordinate is for points.
                    BulletSharp.Math.Vector3 bulletWorldCorner = BulletSharp.Math.Vector3.TransformCoordinate(bulletLocalCorner, worldTransform);
                    // Convert back to OpenTK Vector3
                    worldCorners[i] = new(bulletWorldCorner.X, bulletWorldCorner.Y, bulletWorldCorner.Z);
                }

                // Define the edges connecting the corners (standard cube wireframe)
                int[,] edges = new int[,]
                {
            {0,1}, {1,2}, {2,3}, {3,0}, // bottom face
            {4,5}, {5,6}, {6,7}, {7,4}, // top face
            {0,4}, {1,5}, {2,6}, {3,7}  // vertical edges
                };

                // Draw OBB edges with lines
                for (int i = 0; i < edges.GetLength(0); i++)
                {
                    var start = worldCorners[edges[i, 0]];
                    var end = worldCorners[edges[i, 1]];
                    var c = new Vector4(1, 0, 0, 1);
                    lineRenderer.RenderLine(start, end, c, c);
                }
            }
            else
            {
                body.CollisionShape.GetAabb(body.WorldTransform, out var aabbMin, out var aabbMax);
                Vector3[] aabbWorldCorners = new Vector3[8];
                aabbWorldCorners[0] = new Vector3(aabbMin.X, aabbMin.Y, aabbMin.Z);
                aabbWorldCorners[1] = new Vector3(aabbMax.X, aabbMin.Y, aabbMin.Z);
                aabbWorldCorners[2] = new Vector3(aabbMax.X, aabbMax.Y, aabbMin.Z);
                aabbWorldCorners[3] = new Vector3(aabbMin.X, aabbMax.Y, aabbMin.Z);
                aabbWorldCorners[4] = new Vector3(aabbMin.X, aabbMin.Y, aabbMax.Z);
                aabbWorldCorners[5] = new Vector3(aabbMax.X, aabbMin.Y, aabbMax.Z);
                aabbWorldCorners[6] = new Vector3(aabbMax.X, aabbMax.Y, aabbMax.Z);
                aabbWorldCorners[7] = new Vector3(aabbMin.X, aabbMax.Y, aabbMax.Z);

                int[,] edges = new int[,]
                {
            {0,1}, {1,2}, {2,3}, {3,0},
            {4,5}, {5,6}, {6,7}, {7,4},
            {0,4}, {1,5}, {2,6}, {3,7}
                };
                for (int i = 0; i < edges.GetLength(0); i++)
                {
                    var start = aabbWorldCorners[edges[i, 0]];
                    var end = aabbWorldCorners[edges[i, 1]];
                    lineRenderer.RenderLine(start, end, new(0.5f, 0.5f, 0.5f, 1), new(0.5f, 0.5f, 0.5f, 1));
                }
            }
        }
    }
}