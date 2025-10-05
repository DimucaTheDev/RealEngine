using OpenTK.Mathematics;
using RE.Debug;
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
        public GameObjectList GameObjects { get; } = new();
        public DirectionalLight? SunLight { get; set; } = null; // scene doesnt has sun by default.. maybe

        //TODO: rewrite method, remove "scene loaded"
        /// <remarks>
        /// Calls <see cref="Component.OnSceneLoading(Scene)"/> and <see cref="Component.OnSceneLoaded(Scene)"/> on all components of all game objects in the scene,
        /// </remarks>
        public void Load()
        {
            foreach (var obj in GameObjects)
            {
                foreach (var component in obj.Components)
                {
                    component.OnSceneLoading(this);
                }
            }
            foreach (var obj in GameObjects.ToList())
            {
                foreach (var component in obj.Components.ToList())
                {
                    component.OnSceneLoaded(this);
                }
            }
        }

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
