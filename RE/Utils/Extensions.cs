using RE.Core;
using RE.Core.Physics;
using RE.Rendering;
using System.Numerics;

namespace RE.Utils
{
    public static class Extensions
    {
        // Time
        public static void InvokeNow(this Time.ScheduledTask task)
        {
            task.Action.Invoke();
            Time.RemoveTask(task);
        }
        public static void TerminateIfScheduled(this Time.ScheduledTask? task)
        {
            if (task?.IsScheduled() ?? false)
                Terminate(task);
        }
        public static void Terminate(this Time.ScheduledTask task) => Time.RemoveTask(task);
        // Renderable
        public static void Render<T>(this T r) where T : Renderable => RenderManager.AddRenderable(r);
        public static bool IsRendering<T>(this T r) where T : Renderable
        {
            return RenderManager.Renderables.TryGetValue(r.RenderLayer, out var types) &&
                   types.TryGetValue(typeof(T), out var list) &&
                   list.Contains(r);
        }

        public static void StopRender<T>(this T r) where T : Renderable
        {
            if (r is PhysicObject obj)
            {
                PhysManager.RemovePhysicsObject(obj);
                RenderManager.RemoveRenderable(obj.Model);
                return;
            }
            RenderManager.RemoveRenderable(r);
        }
        // Conversions

        public static OpenTK.Mathematics.Vector3 ToOpenTkVector3(this BulletSharp.Math.Vector3 v) => new(v.X, v.Y, v.Z);
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
        public static OpenTK.Mathematics.Quaternion ToOpenTkQuaternion(this Quaternion q) => new(q.X, q.Y, q.Z, q.W);
        public static Vector3 ToSystemVector3(this OpenTK.Mathematics.Vector3 v) => new(v.X, v.Y, v.Z);
        public static Quaternion ToSystemQuaternion(this OpenTK.Mathematics.Quaternion q) => new(q.X, q.Y, q.Z, q.W);
    }
}
