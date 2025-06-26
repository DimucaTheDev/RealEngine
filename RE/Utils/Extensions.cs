using RE.Rendering;

namespace RE.Utils
{
    internal static class Extensions
    {
        public static void Render<T>(this T r) where T : IRenderable => RenderLayerManager.AddRenderable(r);
    }
}
