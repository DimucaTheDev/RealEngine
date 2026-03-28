using OpenTK.Windowing.Common;

namespace RE.Rendering
{
    public abstract class Renderable : IDisposable
    {
        public abstract RenderLayer RenderLayer { get; }
        public abstract bool IsVisible { get; set; }

        public abstract void Render(FrameEventArgs args);

        // called when Extensions.StartRender() is called
        public virtual void AddedToRenderList() { }
        public virtual void RemovedFromRenderList() { }

        public virtual void Dispose() { }
    }
}
