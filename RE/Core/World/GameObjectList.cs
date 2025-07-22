using System.Collections;

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
            foreach (var component in g.Components)
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
                Game.Instance.RenderFrame -= component.Render;
            }
            _objects.Remove(g);
        }

        public IEnumerator<GameObject> GetEnumerator() => _objects.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
