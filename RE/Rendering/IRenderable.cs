using OpenTK.Windowing.Common;

namespace RE.Rendering
{
    public interface IRenderable
    {
        RenderLayer RenderLayer { get; }
        bool IsVisible { get; set; }
        void Render(FrameEventArgs args);
    }
}
