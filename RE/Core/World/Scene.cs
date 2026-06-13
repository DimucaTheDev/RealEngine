using RE.Editor.Panels.Viewport;
using RE.Rendering;
using RE.Rendering.Lighting;

namespace RE.Core.World
{
    /// <summary>
    /// Represents a scene containing game objects and managing their lifecycle.
    /// </summary>
    public class Scene : IDisposable
    {
        public string? Name { get; set; }
        public GameObjectList GameObjects { get; }
        public List<ILightSource> LightSources { get; set; } = [];

        public Scene() => GameObjects = new GameObjectList(this);
        
        internal readonly List<Component> RenderingComponents = [];


        /// <summary>
        /// Calls <see cref="Component.Destroy"/> on all components of all game objects in the scene and unsubscribes them
        /// from the <c>Update</c> and <c>Render</c> loop.
        /// </summary>
        public void Dispose()
        {
            foreach (var obj in GameObjects)
            {
                ViewportPanel.CollisionWorld.RemoveCollisionObject(obj.ViewportObject);
                foreach (var component in obj.Components)
                {
                    component.Destroy(); 
                    RenderingComponents.Remove(component);
                }
            }
        }
    }
}
