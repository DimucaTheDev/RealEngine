using RE.Rendering;
using RE.Rendering.Lightning;

namespace RE.Core.World
{
    /// <summary>
    /// Represents a scene containing game objects and managing their lifecycle.
    /// </summary>
    public class Scene : IDisposable
    {
        public string? Name { get; set; }
        public GameObjectList GameObjects { get; }
        public DirectionalLight? SunLight { get; set; } = null; // scene doesnt contain sun by default.. maybe

        public Scene() => GameObjects = new GameObjectList(this);

        /// <summary>
        /// Calls <see cref="Component.OnDestroy"/> on all components of all game objects in the scene and unsubscribes them
        /// from the <c>Update</c> and <c>Render</c> loop.
        /// </summary>
        public void Dispose()
        {
            foreach (var obj in GameObjects)
            {
                foreach (var component in obj.Components)
                {
                    component.OnDestroy();
                    Game.Instance.UpdateFrame -= component.Update;
                    RenderManager.RenderingComponents.Remove(component);
                }
            }
        }
    }
}
