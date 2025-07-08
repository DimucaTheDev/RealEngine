using RE.Core.Scripting;

namespace RE.Core.World.Components
{
    internal class UsableComponent : Component
    {
        //todo: add deny and some props
        public Action? OnUsed;
        public UsableComponent() { }
        public UsableComponent(string command) => OnUsed += () => CommandHandler.ExecuteCommand(command);
    }
}
