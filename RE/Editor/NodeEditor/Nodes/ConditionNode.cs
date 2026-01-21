using System;
using System.Collections.Generic;
using System.Text;
using RE.Editor.NodeEditor.Pins;

namespace RE.Editor.NodeEditor.Nodes
{
    internal class ConditionNode : ActionNode
    {
        public override string Name => "Conditional";

        private readonly DataNodePin<bool> _conditionPin;
        private readonly ActionNodePin _truePin;
        private readonly ActionNodePin _falsePin;

        public ConditionNode()
        {
            InputPins.Add(_conditionPin = new DataNodePin<bool>("Condition"));
            OutputPins.Add(_truePin = new ActionNodePin("True"));
            OutputPins.Add(_falsePin = new ActionNodePin("False"));
            ExecuteAction = _ => Execute();
        }

        public void Execute()
        {
            NodeManager.ExecutePin(_conditionPin.Value ? _truePin : _falsePin);
        }
    }
}
