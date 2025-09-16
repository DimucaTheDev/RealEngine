using System.Collections;
using RE.Debug;
using RE.Rendering;

namespace RE.Core.World
{
    public class GameObjectList(Scene scene) : IEnumerable<GameObject>
    {
        private readonly List<GameObject> _objects = new();
        private readonly Scene _scene = scene;

        public void Add(GameObject g)
        {
            _objects.Add(g);
            g.Scene = _scene;
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
            foreach (var component in g.Components)
            {
                Game.Instance.UpdateFrame -= component.Update;
                RenderManager.RenderingComponents.Add(component);
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
