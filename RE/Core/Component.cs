using System.Text.Json.Nodes;
using OpenTK.Windowing.Common;
using RE.Core.World;

namespace RE.Core
{
    public abstract class Component
    {
        protected Component()
        {
            var ctor = GetType().GetConstructor(Type.EmptyTypes);
            if (ctor == null)
                throw new InvalidOperationException("Class must have parameterless constructor.");
        }
        public GameObject Owner { get; internal set; }
        public bool SaveComponent { get; set; } = true;

        public T? GetComponent<T>() where T : Component
        {
            return Owner.Components.OfType<T>().FirstOrDefault();
        }


        public abstract JsonNode GetSaveData();

        public virtual void OnComponentAdded() { }
        public virtual void Start() { }
        public virtual void OnSceneLoading(Scene scene) { }
        public virtual void OnSceneLoaded(Scene scene) { }
        public virtual void Update(FrameEventArgs args) { }
        public virtual void Render(FrameEventArgs args) { }
        public virtual void OnDestroy() { }
        public virtual void OnReset() { }
    }
}
