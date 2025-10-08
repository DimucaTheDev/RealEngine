using System.Collections;

namespace RE.Core.World
{
    /// <summary>
    /// Represents a collection of <see cref="GameObject"/> within a <see cref="Scene"/>.
    /// </summary>
    public class GameObjectList : IEnumerable<GameObject>
    {
        private readonly List<GameObject> _objects = new();

        public void Add(GameObject g, bool doNotCallStart = false)
        {
            _objects.Add(g);
            if (doNotCallStart)
                return;
            //todo: set g.Scene 
            foreach (var component in g.Components.ToList())
            {
                component.Start();
            }
        }

        public void Remove(GameObject g)
        {
            foreach (var child in g.Children.ToList())
            {
                Remove(child);
            }
            foreach (var component in g.Components.ToList())
            {
                g.Components.Remove(component);
            }
            _objects.Remove(g);
        }

        public GameObject FindByTag(string tag) =>
            _objects.First(g => g.Tag!.Equals(tag, StringComparison.InvariantCultureIgnoreCase));
        public GameObject FindByName(string name) =>
            _objects.First(g => g.Name!.Equals(name, StringComparison.InvariantCultureIgnoreCase));

        public IEnumerator<GameObject> GetEnumerator() => _objects.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
