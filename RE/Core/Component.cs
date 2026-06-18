using System.Numerics;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BulletSharp;
using JetBrains.Annotations;
using OpenTK.Windowing.Common;
using RE.Core.Scripting.Attributes;
using RE.Core.World;
using RE.Core.World.Components.Physics;
using RE.Utils;
using Serilog;
using Serilog.Core;
using Vector3 = OpenTK.Mathematics.Vector3;

namespace RE.Core
{
    /// <summary>
    /// Component is the base class for all components that can be attached to a <see cref="GameObject"/>.
    /// </summary>
    /// <remarks>All classes that inherit this class must have parameterless constructor</remarks>
    [UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
    public abstract class Component
    {
        /// <summary>
        /// <see cref="GameObject"/> that this component is attached to.
        /// </summary>
        [JsonIgnore]
        public GameObject Owner { get; internal set; } = null!;

        [EditorProperty]
        public bool IsEnabled
        {
            get;
            set
            {
                field = value;
                if (value)
                    Enabled();
                else
                    Disabled();
            }
        } = true;

        /// <summary>
        /// Whether this component should be stored when the scene is being saved (see <see cref="SceneManager.SaveSceneToFile"/>).
        /// </summary>
        [JsonIgnore]
        public bool SaveComponent { get; set; } = true;

        // false if should component be rendered to oit texture
        public virtual bool IsOpaque { get; } = true;

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
        public virtual JsonNode GetSaveData() => GetDataForProperties(this);

        private JsonNode GetDataForProperties(object instance)
        {
            JsonObject obj = new();
            var properties
                = GetType()
                    .GetProperties()
                    .Where(p =>
                        p is { CanRead: true, CanWrite: true }
                        && p.GetCustomAttribute<JsonIgnoreAttribute>() == null
                        && (p.GetCustomAttribute<JsonIncludeAttribute>() != null ||
                            p.GetCustomAttribute<EditorPropertyAttribute>() != null))
                    .Select(p => (Name: p.Name, Value: p.GetValue(this)));
            var fields
                = GetType()
                    .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                    .Where(p => p.GetCustomAttribute<JsonIncludeAttribute>() != null)
                    .Select(p =>
                        (Name: p.GetCustomAttribute<JsonPropertyNameAttribute>() is { } a ? a.Name : p.Name,
                            Value: p.GetValue(this)));


            foreach ((string name, object? value) in properties.Concat(fields))
            {
                if (value is null)
                    obj.Add(name, null);
                else if (value is Vector3 v)
                    obj.Add(name, v.ToJsonArray());
                else
                    obj.Add(name, JsonValue.Create(value));
            }

            return obj;
        }


        /// <summary>
        /// Called when component is added to a game object.
        /// </summary>
        public virtual void Start()
        {
        }

        /// <summary>
        /// Called every frame, before <see cref="Render(FrameEventArgs)"/>.
        /// </summary>
        /// <param name="args">The event data for the frame being rendered, containing timing and state information relevant to the
        /// rendering process.</param>
        public virtual void Update(FrameEventArgs args)
        {
        }

        /// <summary>
        /// Renders the current frame using the specified frame event arguments.
        /// </summary>
        /// <param name="args">The event data for the frame being rendered, containing timing and state information relevant to the
        /// rendering process.</param>
        public virtual void Render(FrameEventArgs args)
        {
        }

        protected virtual void Enabled()
        {
        }

        protected virtual void Disabled()
        {
        }

        /// <summary>
        /// Performs cleanup operations when the object is being destroyed. Override this method to release resources or
        /// unsubscribe from events as needed.
        /// </summary>
        /// <remarks>This method is intended to be overridden in derived classes to implement custom
        /// destruction logic. The base implementation does not perform any actions.</remarks>
        public virtual void Destroy()
        {
        }

        public virtual CollisionShape GetObjectSelectionShape() => new BoxShape(0.3f);

        /// <summary>
        /// Called when an object collided with another object
        /// </summary>
        /// <param name="collide">Another object that this object collides with</param>
        /// <remarks>Method is only called if <see cref="Owner"/> has any <see cref="ColliderComponent"/></remarks>
        public virtual void OnCollisionEnter(GameObject collide)
        {
        }

        /// <summary>
        /// Called when an object is being collided with another object in scene
        /// </summary>
        /// <param name="collide">Another object that this object collides with</param>
        /// <remarks>Method is only called if <see cref="Owner"/> has any <see cref="ColliderComponent"/></remarks>
        public virtual void OnCollide(GameObject collide)
        {
        }

        /// <summary>
        /// Called when an object stops colliding with another object
        /// </summary>
        /// <param name="collide">Another object that this object stopped collided with</param>
        /// <remarks>Method is only called if <see cref="Owner"/> has any <see cref="ColliderComponent"/></remarks>
        public virtual void OnCollisionExit(GameObject collide)
        {
        }

        public virtual void PhysicsSync()
        {
        }
    }
}