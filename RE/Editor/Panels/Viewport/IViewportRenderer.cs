using OpenTK.Mathematics;

namespace RE.Editor.Panels.Viewport
{
    internal interface IViewportRenderer
    {
        public void ViewportRender(Vector2 pos, Vector2 size);
    }
}
