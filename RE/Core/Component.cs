using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core.World;
using RE.Utils;

namespace RE.Core
{
    /// <summary>
    /// Component is the base class for all components that can be attached to a <see cref="GameObject"/>.
    /// </summary>
    /// <remarks>All classes that inherit this class must have parameterless constructor</remarks>
    public abstract class Component
    {
        /// <summary>
        /// <see cref="GameObject"/> that this component is attached to.
        /// </summary>
        [JsonIgnore]
        public GameObject Owner { get; internal set; } = null!;
        /// <summary>
        /// Whether this component should be stored when the scene is being saved (see <see cref="SceneManager.SaveScene"/>).
        /// </summary>
        [JsonIgnore]
        public bool SaveComponent { get; set; } = true;

        /// <typeparam name="T">Component type</typeparam>
        /// <returns>Returns the first component of type T that is attached to the same <see cref="GameObject"/> as this component, or null if no such component exists.</returns>
        public T? GetComponent<T>() where T : Component
        {
            return Owner.Components.OfType<T>().FirstOrDefault();
        }

        /// <summary>
        /// Get the data to be saved for this component.
        /// </summary>
        /// <remarks>This method is not called if <see cref="SaveComponent"/> is set to <see langword="false"/></remarks>
        /// <returns><see cref="JsonNode"/> that contains saved data</returns>
        public abstract JsonNode GetSaveData();

        protected JsonNode GetDataForProperties()
        {
            JsonObject obj = new();
            var properties = GetType().GetProperties().Where(p => p is { CanRead: true, CanWrite: true });
            foreach (var property in properties.Where(s => s.GetCustomAttribute<JsonIgnoreAttribute>() == null))
            {
                var value = property.GetValue(this);
                if (value is null)
                    obj.Add(property.Name, null);
                else if (value is Vector3 v)
                    obj.Add(property.Name, v.ToJsonArray());
                else
                    obj.Add(property.Name, JsonValue.Create(value));

            }

            return obj;
        }


        /// <summary>
        /// Called when component is added to a game object.
        /// </summary>
        public virtual void Start() { }
        /// <summary>
        /// Called every frame, before <see cref="Render(FrameEventArgs)"/>.
        /// </summary>
        /// <param name="args">The event data for the frame being rendered, containing timing and state information relevant to the
        /// rendering process.</param>
        public virtual void Update(FrameEventArgs args) { }
        /// <summary>
        /// Renders the current frame using the specified frame event arguments.
        /// </summary>
        /// <param name="args">The event data for the frame being rendered, containing timing and state information relevant to the
        /// rendering process.</param>
        public virtual void Render(FrameEventArgs args) { }
        /// <summary>
        /// Performs cleanup operations when the object is being destroyed. Override this method to release resources or
        /// unsubscribe from events as needed.
        /// </summary>
        /// <remarks>This method is intended to be overridden in derived classes to implement custom
        /// destruction logic. The base implementation does not perform any actions.</remarks>
        public virtual void OnDestroy() { }
    }
}
