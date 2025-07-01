using RE.Core;
using RE.Rendering;

namespace RE.Utils
{
    public static class Extensions
    {
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
        public static void Render<T>(this T r) where T : IRenderable => RenderLayerManager.AddRenderable(r);
        public static bool IsRendering<T>(this T r) where T : IRenderable
        {
            return RenderLayerManager.Renderables.TryGetValue(r.RenderLayer, out var types) &&
                   types.TryGetValue(typeof(T), out var list) &&
                   list.Contains(r);
        }
        public static void StopRender<T>(this T r) where T : IRenderable => RenderLayerManager.RemoveRenderable(r);
    }
}
