using Hexa.NET.ImGui;
using RE.Editor.NodeEditor.Pins;

namespace RE.Editor.NodeEditor.Nodes
{
    internal class StartNode : ActionNode
    {
        public override string Name => "OnStart";

        public StartNode()
        {
            InputPins.Clear();
            OutputPins.Clear();

       //     OutputPins.Add(new ActionNodePin("Exec", this));
        }

        public override void RenderNode()
        {
            base.RenderNode();
            if (ImGui.Button("Execute"))
            {
                NodeManager.ResetAll();
                NodeManager.ExecuteNode(this);
            }
        }

       // public override void Execute()
       // {
       //     Console.WriteLine("Start");
       // }
    }
}
