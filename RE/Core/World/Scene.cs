namespace RE.Core.World
{
    public class Scene : IDisposable
    {
        public string? Name { get; set; }
        public GameObjectList GameObjects { get; }

        public Scene() => GameObjects = new GameObjectList(this);

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

        public void Dispose()
        {
            foreach (var obj in GameObjects)
            {
                foreach (var component in obj.Components)
                {
                    component.OnDestroy();
                    Game.Instance.UpdateFrame -= component.Update;
                    Game.Instance.RenderFrame -= component.Render;
                }
            }
        }
    }
}
