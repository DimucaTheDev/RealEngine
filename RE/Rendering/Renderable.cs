namespace RE.Rendering
{
    public abstract class Renderable : IRenderable, IDisposable
    {
        public abstract RenderLayer RenderLayer { get; }
        public abstract bool IsVisible { get; set; }
        public bool UseCulling { get; set; } = true;
        public abstract void Render(OpenTK.Windowing.Common.FrameEventArgs args);

        // called when Extensions.Render() is called
        public virtual void AddedToRenderList() { }
        public virtual void RemovedFromRenderList() { }

        public virtual void Dispose() { }
    }
}
