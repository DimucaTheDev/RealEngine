using System.Text.Json.Nodes;
using BulletSharp;
using Hexa.NET.ImGui;
using OpenTK.Mathematics;
using RE.Core;
using RE.Core.World.Physics;
using RE.Rendering;
using Quaternion = System.Numerics.Quaternion;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace RE.Utils
{
    public static class Extensions
    {
        public static MemoryStream AsMemoryStream(this Stream s)
        {
            var ms = new MemoryStream();
            s.CopyTo(ms);
            ms.Position = 0;
            return ms;
        }

        extension(Color c)
        {
            public uint ToImGuiColor()
            {
                return ImGui.ColorConvertFloat4ToU32(new Vector4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f));
            }
        }
        extension(Color4 c)
        {
            public Vector4 ToVec4Color()
            {
                return new Vector4(c.R, c.G, c.B, c.A);
            }
            public OpenTK.Mathematics.Vector4 ToTkVec4Color()
            {
                return new(c.R, c.G, c.B, c.A);
            }
            public uint ToImGuiColor()
            {
                return ImGui.ColorConvertFloat4ToU32(new Vector4(c.R, c.G, c.B, c.A));
            }
        }

        // Time
        extension(Time.ScheduledTask task)
        {
            public void InvokeNow()
            {
                task.Action.Invoke();
                Time.RemoveTask(task);
            }
            public void TerminateIfScheduled()
            {
                if (task?.IsScheduled() ?? false)
                    Terminate(task);
            }
            public void Terminate() => Time.RemoveTask(task);
        }

        // Renderable
        extension<T>(T r) where T : Renderable
        {
            public void StartRender() => RenderManager.AddRenderable(r);
            public bool IsRendering()
            {
                return RenderManager.Renderables.TryGetValue(r.RenderLayer, out var types) &&
                       types.TryGetValue(typeof(T), out var list) &&
                       list.Contains(r);
            }
            public void StopRender()
            {
                RenderManager.RemoveRenderable(r);
            }
        }

        extension(RigidBody r)
        {
            public void Disable() => PhysicsManager.DynamicsWorld.RemoveRigidBody(r);
            public void Enable() => PhysicsManager.DynamicsWorld.AddRigidBody(r);
        }

        // Conversions

        public static OpenTK.Mathematics.Vector3 ToOpenTkVector3(this BulletSharp.Math.Vector3 v) => new(v.X, v.Y, v.Z);
        public static OpenTK.Mathematics.Vector3 ToOpenTkVector3(this Assimp.Vector3D v) => new(v.X, v.Y, v.Z);
        public static OpenTK.Mathematics.Vector4 ToOpenTkVector4(this BulletSharp.Math.Vector4 v) => new(v.X, v.Y, v.Z, v.W);
        public static OpenTK.Mathematics.Quaternion ToOpenTkQuaternion(this BulletSharp.Math.Quaternion q) => new(q.X, q.Y, q.Z, q.W);
        public static BulletSharp.Math.Vector3 ToBulletVector3(this OpenTK.Mathematics.Vector3 v) => new(v.X, v.Y, v.Z);
        public static BulletSharp.Math.Vector4 ToBulletVector4(this OpenTK.Mathematics.Vector4 v) => new(v.X, v.Y, v.Z, v.W);
        public static BulletSharp.Math.Quaternion ToBulletQuaternion(this OpenTK.Mathematics.Quaternion q) => new(q.X, q.Y, q.Z, q.W);
        public static OpenTK.Mathematics.Matrix4 ToOpenTkMatrix(this BulletSharp.Math.Matrix v) =>
            new(v.M11, v.M12, v.M13, v.M14, v.M21, v.M22, v.M23, v.M24, v.M31, v.M32, v.M33, v.M34, v.M41, v.M42, v.M43, v.M44);
        public static BulletSharp.Math.Matrix ToBulletMatrix(this OpenTK.Mathematics.Matrix4 v) =>
            new(v.M11, v.M12, v.M13, v.M14, v.M21, v.M22, v.M23, v.M24, v.M31, v.M32, v.M33, v.M34, v.M41, v.M42, v.M43, v.M44);
        public static OpenTK.Mathematics.Vector3 ToOpenTkVector3(this Vector3 v) => new(v.X, v.Y, v.Z);
        public static OpenTK.Mathematics.Vector3 ToOpenTkVector3(this FMOD.VECTOR v) => new(v.x, v.y, v.z);
        public static OpenTK.Mathematics.Vector2 ToOpenTkVector2(this Vector2 v) => new(v.X, v.Y);
        public static OpenTK.Mathematics.Quaternion ToOpenTkQuaternion(this Quaternion q) => new(q.X, q.Y, q.Z, q.W);
        public static Vector3 ToSystemVector3(this OpenTK.Mathematics.Vector3 v) => new(v.X, v.Y, v.Z);
        public static Vector3 ToSystemVector3(this FMOD.VECTOR v) => new(v.x, v.y, v.z);
        public static Vector2 ToSystemVector2(this OpenTK.Mathematics.Vector2 v) => new(v.X, v.Y);
        public static FMOD.VECTOR ToFmodVector3(this OpenTK.Mathematics.Vector3 v) => new() { x = v.X, y = v.Y, z = v.Z };
        public static Quaternion ToSystemQuaternion(this OpenTK.Mathematics.Quaternion q) => new(q.X, q.Y, q.Z, q.W);
        public static JsonArray ToJsonArray(this OpenTK.Mathematics.Vector3 v) => new(v.X, v.Y, v.Z);
    }
}
