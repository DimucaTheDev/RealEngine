using System.Text.Json.Nodes;
using RE.Core;
using RE.Core.Logging;
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
            ToastManager.InsertNotification(new Toast(ToastType.Success, "Hello from loaded plugin!", "It works!", 3));
        }
    }
}
