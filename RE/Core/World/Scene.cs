namespace RE.Core.World
{
    internal class Scene : IDisposable
    {
        public string? Name { get; set; }
        public GameObjectList GameObjects { get; } = [];

        public void Load()
        {
            foreach (var obj in GameObjects)
            {
                foreach (var component in obj.Components)
                {
                    //see GameObjectList.Add()

                    //component.Start();
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
