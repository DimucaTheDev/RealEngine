using System.Collections;

#pragma warning disable 108

namespace RE.Core.World.Components
{
    internal class ComponentList(GameObject owner) : IEnumerable<Component>
    {
        private readonly GameObject _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        private readonly List<Component> _components = new();

        public void Add(Component c)
        {
            if (c == null)
                throw new ArgumentNullException(nameof(c), "Component cannot be null");

            if (c.Owner != null)
                throw new InvalidOperationException("Component already has an owner");

            if (_components.Any(s => s.GetType() == c.GetType()))
                throw new InvalidOperationException("Component is already in this list");

            c.Owner = _owner;

            Game.Instance.UpdateFrame += c.Update;
            Game.Instance.RenderFrame += c.Render;

            _components.Add(c);
        }

        public void Remove(Component c)
        {
            if (c == null!)
                return;

            if (!_components.Contains(c))
                return;

            Game.Instance.UpdateFrame -= c.Update;
            Game.Instance.RenderFrame -= c.Render;

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
