using RE.Core;
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
        public static void StopRender<T>(this T r) where T : Renderable => RenderManager.RemoveRenderable(r);
        // Conversions
        public static OpenTK.Mathematics.Vector3 ToOpenTkVector3(this Vector3 v) => new(v.X, v.Y, v.Z);
        public static OpenTK.Mathematics.Quaternion ToOpenTkQuaternion(this Quaternion q) => new(q.X, q.Y, q.Z, q.W);
        public static Vector3 ToSystemVector3(this OpenTK.Mathematics.Vector3 v) => new(v.X, v.Y, v.Z);
        public static Quaternion ToSystemQuaternion(this OpenTK.Mathematics.Quaternion q) => new(q.X, q.Y, q.Z, q.W);
    }
}
