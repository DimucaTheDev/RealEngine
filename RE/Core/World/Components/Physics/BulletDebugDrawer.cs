using BulletSharp;
using BulletSharp.Math;
using OpenTK.Graphics.OpenGL4;
using RE.Debug;
using RE.Utils;
using Vector4 = OpenTK.Mathematics.Vector4;

namespace RE.Core.World.Components.Physics
{
    public class BulletDebugDrawer : BulletSharp.DebugDraw
    {
        public static DebugDrawModes Mode = DebugDrawModes.None;

        public override DebugDrawModes DebugMode { get => Mode; set => Mode = value; }
        public static bool Xray { get; set; }

        public override void DrawLine(ref Vector3 from, ref Vector3 to, ref Vector3 color)
        {
            if (Xray)
                GL.Disable(EnableCap.DepthTest);
            var c = new Vector4(color.X, color.Y, color.Z, 1);
            LineRenderer.DrawLine(from.ToOpenTkVector3(), to.ToOpenTkVector3(), c, c);
            GL.Enable(EnableCap.DepthTest);
        }

        public override void Draw3DText(ref Vector3 location, string textString)
        {
            throw new NotImplementedException();
        }

        public override void ReportErrorWarning(string warningString)
        {
            throw new NotImplementedException();
        }

    }
}
