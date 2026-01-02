using System.Text.Json.Nodes;
using RE.Core.Scripting;
using RE.Core.Scripting.Attributes;

namespace RE.Core.World.Components
{
    [ComponentInfo("World/Scripting", Description = "Triggers actions or commands when the player presses 'E' while looking at the object")]
    internal class UsableComponent : Component
    { 
        public Action? OnUsed;
        public UsableComponent() { }
        public UsableComponent(string command) => Command = command;

        [EditorProperty(IsReadOnly = true)] public string Subscribers => $"{OnUsed?.GetInvocationList().Length} Total";
        [EditorProperty(DisplayedName = "Invoke Command")] public string Command { get; set; } = null!;

        public override void Start()
        { 
            OnUsed -= ExecuteCommandOnUse; // Prevent double subscription
            OnUsed += ExecuteCommandOnUse;
        }

        private void ExecuteCommandOnUse() { CommandHandler.ExecuteCommand(Command); }
         
        public override JsonNode GetSaveData()
        {
            JsonObject root = new();
            if (Command != null!)
            {
                var args = new JsonArray { Command };
                root.Add("args", args);
            }

            return root;
        }
    }
}
