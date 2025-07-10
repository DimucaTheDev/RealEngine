using System.Collections;

namespace RE.Core.World
{
    public class GameObjectList(Scene scene) : IEnumerable<GameObject>
    {
        private readonly List<GameObject> _components = new();
        private readonly Scene _scene = scene;

        public void Add(GameObject g)
        {
            _components.Add(g);
            g.Scene = _scene;
            foreach (var component in g.Components)
            {
                component.Start();
            }
        }

        public void Remove(GameObject g)
        {
            foreach (var component in g.Components)
            {
                Game.Instance.UpdateFrame -= component.Update;
                Game.Instance.RenderFrame -= component.Render;
            }
            _components.Remove(g);
        }

        public IEnumerator<GameObject> GetEnumerator() => _components.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
