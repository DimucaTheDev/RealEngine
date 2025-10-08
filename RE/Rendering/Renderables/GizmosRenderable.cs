using System;
using System.Collections.Generic;
using System.Text;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core.World;
using RE.Utils;

namespace RE.Rendering.Renderables
{
    public class GizmosRenderable : ModelRenderer
    {
        public enum Axis
        {
            X, Y, Z
        }

        public GizmosRenderable(GameObject? g, Axis a) : base("assets/models/axis.fbx", Vector3.PositiveInfinity)
        {
            IgnoreLight = true;
            ConstantSize = true;
            GameObject = g!;
            _axis = a;
            SetTexture(Util.CreateMonoColorTexture(
                a switch
                {
                    Axis.X => new(1, 0, 0),
                    Axis.Y => new(0, 1, 0),
                    Axis.Z => new(0, 0, 1),
                }));
        }

        public GameObject GameObject { get; set; }
        private Axis _axis;

        public override void Render(FrameEventArgs args)
        {
            if(GameObject == null!) return;
            GL.Disable(EnableCap.DepthTest);
            var min = MinBounds;
            var max = MaxBounds;
            var objPos = GameObject.Transform.Position;

            var offset = ((max - min) / 2).X;
            var camPos = Camera.Instance.Position;
            float distance = (camPos - objPos).Length;
            float fixedScale = distance * 0.05f;
            Matrix4 model = Matrix4.Zero;

            switch (_axis)
            {
                case Axis.X:
                    Position = objPos + new Vector3(offset * fixedScale, 0, 0);
                    model =
                        Matrix4.CreateScale(fixedScale) *
                        Matrix4.CreateTranslation(Position);
                    break;
                case Axis.Y:

                    Position = objPos + new Vector3(0, offset * fixedScale, 0);
                    model =
                        Matrix4.CreateScale(fixedScale) *
                        Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(90)) *
                        Matrix4.CreateTranslation(Position);
                    break;
                case Axis.Z:
                    Position = objPos + new Vector3(0, 0, offset * fixedScale);
                    model =
                        Matrix4.CreateScale(fixedScale) *
                        Matrix4.CreateRotationY(MathHelper.DegreesToRadians(-90)) *
                        Matrix4.CreateTranslation(Position);
                    break;
            }
            Render(args, model);
            GL.Disable(EnableCap.DepthTest);
        }
    }
}
