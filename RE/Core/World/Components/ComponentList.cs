using System.Collections;
using RE.Rendering;

#pragma warning disable 108

namespace RE.Core.World.Components
{
    /// <summary>
    /// Represents a collection of components associated with a specific game object. Provides methods to add, remove,
    /// and enumerate components attached to the owner object.
    /// </summary>
    /// <remarks>The ComponentList manages the lifecycle of components for its owner, including ownership
    /// assignment and event registration. Components added to this list must not already have an owner, and only one
    /// component of each type can be added.</remarks>
    /// <param name="owner">The game object that owns the components in this list. Cannot be null.</param>
    public class ComponentList(GameObject owner) : IEnumerable<Component>
    {
        private readonly GameObject _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        private readonly List<Component> _components = new();

        public bool TryAdd(Component c)
        {
            try
            {
                Add(c);
            }
            catch
            {
                return false;
            }

            return true;
        }
        /// <summary>
        /// Adds a component to the list, assigning its owner and registering it for updates and rendering.
        /// </summary>
        /// <param name="c">Component instance to be added.</param>
        /// <param name="doNotCallStart">Whether call <see cref="Component.Start"/> method after adding to the object.</param>
        /// <exception cref="ArgumentNullException">Provided component instance is null</exception>
        /// <exception cref="InvalidOperationException">
        /// Component already has an owner -or- Component of the same type is already in this list
        /// </exception>
        public void Add(Component c, bool doNotCallStart = false)
        {
            if (c == null)
                throw new ArgumentNullException(nameof(c), "Component cannot be null");

            if (c.Owner != null)
                throw new InvalidOperationException("Component already has an owner");

            if (_components.Any(s => s.GetType() == c.GetType()))
                throw new InvalidOperationException("Component is already in this list");

            c.Owner = _owner;

            Game.Instance.UpdateFrame += c.Update;
            RenderManager.RenderingComponents.Add(c);
            _components.Add(c);
            if (!doNotCallStart)
                c.Start(); 
        }

        /// <summary>
        /// Removes the specified component from the collection and detaches it from the game loop and rendering system.
        /// </summary>
        /// <remarks>This method unsubscribes the component from update and rendering events and calls its
        /// destruction logic before removal. After removal, the component's owner is set to null.</remarks>
        /// <param name="c">The component to remove from the collection. If the component is null or not present in the collection, the
        /// method has no effect.</param>
        public void Remove(Component c)
        {
            if (c == null!)
                return;

            if (!_components.Contains(c))
                return;

            Game.Instance.UpdateFrame -= c.Update;
            RenderManager.RenderingComponents.Remove(c);
            c.OnDestroy();

            _components.Remove(c);

            c.Owner = null!; 
        }
        public void Clear()
        {
            foreach (var component in _components)
            {
                Remove(component);
            }
            _components.Clear();
        }
        public Component this[int index] => _components[index];
        public int Count => _components.Count;

        public IEnumerator<Component> GetEnumerator() => _components.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
