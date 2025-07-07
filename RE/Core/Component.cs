using OpenTK.Windowing.Common;
using RE.Core.World;

namespace RE.Core
{
    internal abstract class Component
    {
        public GameObject Owner { get; internal set; }

        public T GetComponent<T>() where T : Component
        {
            return (T)Owner.Components.FirstOrDefault(s => s is T)!;
        }

        public virtual void Start() { }
        public virtual void Update(FrameEventArgs args) { }
        public virtual void Render(FrameEventArgs args) { }
        public virtual void OnDestroy() { }
    }
}
