using System;
using System.Collections.Generic;
using System.Text;
using OpenTK.Mathematics;
using RE.Editor.NodeEditor.Pins;

namespace RE.Editor.NodeEditor.Nodes
{
    internal class NodeBuilder(NodeBuilder.NodeType type)
    {
        internal enum NodeType
        {
            //todo: rename
            Data,
            Func
        }

        private readonly Node _node = type == NodeType.Data ? new DataNode() : new ActionNode();

        public NodeBuilder SetColor(Color4 color)
        {
            _node.NodeColor = color;
            return this;
        }

        public NodeBuilder SetTitle(string title)
        {
            _node.Name = title;
            return this;
        }

        public NodeBuilder AddInputPin(NodePin pin)
        {
            _node.InputPins.Add(pin);
            return this;
        }

        public NodeBuilder AddOutputPin(NodePin pin)
        {
            _node.OutputPins.Add(pin);
            return this;
        }

        public NodeBuilder SetExecutionAction(Action<Node> executeAction)
        {
            _node.ExecuteAction = executeAction;
            return this;
        }

        public Node Build()
        {
            return _node;
        }
    }
}
