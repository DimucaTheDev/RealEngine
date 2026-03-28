using BulletSharp.Math;
using OpenTK.Windowing.Common;
using RE.Core.World.Components;
using RE.Core.World.Components.Physics;
using RE.Rendering;
using RE.Utils;

namespace RE.Core.World.Testing
{
    internal class Player2 : PlayerComponent
    {
        public override void Update(FrameEventArgs args)
        {
            base.Update(args);
            if (Game.Instance.MouseState.IsButtonPressed(0))
            {
                var forward = Camera.Main.Front.Normalized();

                // Сила отдачи
                float recoilForce = 16f;

                // Отдача назад
                var impulse = new Vector3(
                    -forward.X * recoilForce,
                    -forward.Y * recoilForce,
                    -forward.Z * recoilForce
                );
                PlayerGameObject.GetComponent<RigidBodyComponent>()!.RigidBody.ApplyCentralImpulse(impulse);
            }
        }
    }

}
