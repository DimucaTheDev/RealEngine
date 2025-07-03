using BulletSharp;
using BulletSharp.Math;
using OpenTK.Windowing.Common;
using RE.Debug;
using RE.Rendering;
using RE.Rendering.Renderables;
using Quaternion = OpenTK.Mathematics.Quaternion;
using Vector3 = OpenTK.Mathematics.Vector3;
using Vector4 = OpenTK.Mathematics.Vector4;

namespace RE.Core.Physics
{
    //todo: make class abstract, make SimplePhysicObject or smth like that
    internal class PhysicObject : Renderable
    {
        public ModelRenderer Model { get; set; }
        public RigidBody RigidBody { get; private set; }
        public override RenderLayer RenderLayer => RenderLayer.World;
        public override bool IsVisible { get; set; } = true;

        public PhysicObject(ModelRenderer model, RigidBody rigidBody)
        {
            Model = model;
            RigidBody = rigidBody;
            RigidBody.UserObject = this;
        }

        public virtual void OnColliderEnter(PhysicObject obj) { }
        public virtual void OnColliderStay(PhysicObject obj) { }
        public virtual void OnColliderExit(PhysicObject obj) { }

        public override void Render(FrameEventArgs args)
        {
            RigidBody.GetWorldTransform(out Matrix bulletTransform);
            Model.Position = new Vector3(bulletTransform.Origin.X, bulletTransform.Origin.Y, bulletTransform.Origin.Z);
            BulletSharp.Math.Quaternion bulletRotation = BulletSharp.Math.Quaternion.RotationMatrix(bulletTransform.Basis);
            Model.Rotation = new Quaternion(bulletRotation.X, bulletRotation.Y, bulletRotation.Z, bulletRotation.W);
            Model.Render(args);
            // DrawRigidBodyBounds(RigidBody, LineManager.Main!);
        }

        void DrawRigidBodyBounds(RigidBody body, LineManager lineManager)
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
                    lineManager.AddLine(start, end, c, c, (int)((float)1 / 70 * 1000));
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
                    lineManager.AddLine(start, end, new(0.5f, 0.5f, 0.5f, 1), new(0.5f, 0.5f, 0.5f, 1), (int)((float)1 / 70 * 1000));
                }
            }
        }
        public override void Dispose()
        {
            RigidBody?.Dispose();
            RigidBody = null;
            base.Dispose();
        }
    }
}