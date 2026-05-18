using System.Diagnostics;
using BulletSharp;
using BulletSharp.Math;
using OpenTK.Windowing.Common;
using RE.Core.Scripting.Attributes;
using RE.Core.World.Physics;
using RE.Core.World.Testing;
using RE.Utils;
using Vector3 = OpenTK.Mathematics.Vector3;

namespace RE.Core.World.Components.Physics
{
    [RequiresComponent<RigidBodyComponent>]
    internal abstract class ColliderComponent : Component
    {
        public abstract CollisionShape CreateCollisionShape();
    }
}