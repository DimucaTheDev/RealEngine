using System.Text.Json.Nodes;
using RE.Core;
using RE.Core.World.Components;

namespace TestPlugin
{
    public class TestComponent : Component
    {
        public override void Start()
        {
            var gameObject = Owner;
            gameObject.Components.TryAdd(new BillboardTextComponent($"If you see this text,\nthen this means plugin\nsystem works!\n{TestPlugin.Instance.PluginInformation.Name} ({TestPlugin.Instance.PluginInformation.Version})")
            {
                SaveComponent = false
            });
        }

        public override JsonNode GetSaveData()
        {
            return new JsonObject();
        }
    }
}
