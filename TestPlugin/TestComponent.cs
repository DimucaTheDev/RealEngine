using System.Text.Json.Nodes;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using RE.Core;
using RE.Core.Input;
using RE.Core.World.Components;
using RE.Editor;

namespace TestPlugin
{
    public class TestComponent : Component, IEditorUpdate
    {
        public override void Start()
        {
            var gameObject = Owner;
            gameObject.Components.TryAdd(new BillboardTextComponent($"If you see this text,\nthen this means plugin\nsystem works!\n{TestPlugin.Instance.PluginInformation.Name} ({TestPlugin.Instance.PluginInformation.Version})")
            {
                SaveComponent = false
            });
        }

        /// <inheritdoc />
        public override void Update(FrameEventArgs args)
        {
            if(Keyboard.IsKeyPressed(Keys.LeftControl, true)) Console.WriteLine("Control!");
        }

        public override JsonNode GetSaveData()
        {
            return new JsonObject();
        }

        /// <inheritdoc />
        public void EditorUpdate(FrameEventArgs args)
        {
            Update(args);
        }
    }
}
