using System.Collections;

namespace RE.Core.World
{
    internal class GameObjectList : IEnumerable<GameObject>
    {
        private readonly List<GameObject> _components = new();

        public void Add(GameObject g)
        {
            _components.Add(g);
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
