using System;
using System.Collections.Generic;
using System.Text;
using RE.Editor.NodeEditor.Pins;

namespace RE.Editor.NodeEditor.Nodes
{
    internal class EqualsNodes : DataNode
    {
        public override string Name => "==";
        private readonly DataNodePin<object> _inputA;
        private readonly DataNodePin<object> _inputB;
        private readonly DataNodePin<bool> _output;
        public EqualsNodes()
        {
            //_inputA = new("A", this);
            //_inputB = new("B", this);
            //_output = new("Result", this);
            InputPins.AddRange(_inputA, _inputB);
            OutputPins.Add(_output);
        }

        //public override void Execute()
        //{
        //    _output.Value = object.Equals(_inputA.Value, _inputB.Value);
        //}
    }
}
