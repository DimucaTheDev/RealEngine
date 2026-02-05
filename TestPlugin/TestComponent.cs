using System.Text.Json.Nodes;
using RE.Core;
using RE.Core.World.Components;
using RE.Editor.Notification;

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
            ToastManager.InsertNotification(new Toast(ToastType.Error, "", "MALFUNCTION 54"));
            string content = """
                             ~   - Open Console (type 'help')
                             E   - Interact
                             F2  - Make screenshot
                             F3  - Wireframe
                             F4  - Open Editor
                             F7  - Toast test
                             F8  - Disable light
                             F11 - Fullscreen
                             """;
            ToastManager.InsertNotification(new Toast(ToastType.Info, content, "Welcome! Welcome to City 17.", 10));

        }
        public override JsonNode GetSaveData()
        {
            return new JsonObject();
        }
    }
}
