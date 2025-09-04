using System.Text.Json.Nodes;
using RE.Core;
using RE.Core.World;
using RE.Core.World.Components;

namespace TestPlugin
{
    public class TestComponent : Component
    {
        public override void OnSceneLoaded(Scene s)
        {
            var gameObject = Owner;
            gameObject.Components.Add(new BillboardTextComponent($"If you see this text,\nthen this means plugin\nsystem works!\n{TestPlugin.Instance.PluginInformation.Name} ({TestPlugin.Instance.PluginInformation.Version})"));
        }

        public override JsonNode GetSaveData()
        {
            return new JsonObject();
        }
    }
}
