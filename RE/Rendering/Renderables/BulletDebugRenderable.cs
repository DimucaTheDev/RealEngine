using OpenTK.Windowing.Common;
using RE.Core.World.Physics;

namespace RE.Rendering.Renderables
{
    internal class BulletDebugRenderable : Renderable
    { 
        public override bool IsVisible { get; set; }
        public override void Render(double args)
        {
            PhysicsManager.DynamicsWorld.DebugDrawWorld();
        }
    }
}
