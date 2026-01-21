using System;
using System.Collections.Generic;
using System.Text;
using RE.Editor.NodeEditor.Pins;

namespace RE.Editor.NodeEditor.Nodes
{
    internal class WaitForAllNode : ActionNode
    {
        public override string Name => "Wait For All";
        public WaitForAllNode()
        {
            InputPins.Clear();
            //InputPins.Add(new ActionNodePin($"In 1", this));
        }

        private HashSet<int> _receivedSignals = new();

        public override void RenderNode()
        {
            base.RenderNode();
            var emptyPins = InputPins.Where(p => !p.GetLinkedPins().Any()).ToList();
            int linkedCount = InputPins.Count - emptyPins.Count;
            int currentTotal = InputPins.Count;

            bool changed = false;

            if (linkedCount == currentTotal)
            {
                //InputPins.Add(new ActionNodePin($"In {currentTotal + 1}", this));
                changed = true;
            }
            else if (emptyPins.Count >= 2)
            {
                InputPins.Remove(emptyPins[0]);
                changed = true;
            }

            if (changed)
            {
                for (int i = 0; i < InputPins.Count; i++)
                {
                    InputPins[i].Name = $"In {i + 1}";
                }
            }
        }

        public bool RegisterSignal(int pinId)
        {
            _receivedSignals.Add(pinId);

            int connectedInputs = InputPins.Count(p => p.GetLinkedPins().Any());

            if (_receivedSignals.Count >= connectedInputs)
            {
                _receivedSignals.Clear();
                return true;
            }

            return false;
        }
    }
}
