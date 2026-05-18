using OpenTK.Windowing.Common;

namespace RE.Rendering
{
    public abstract class Renderable
    { 
        public abstract bool IsVisible { get; set; }

        public abstract void Render(FrameEventArgs args); 

        public virtual void Dispose() { }
    }
}
