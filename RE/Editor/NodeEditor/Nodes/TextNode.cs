using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Hexa.NET.ImGui;
using RE.Editor.NodeEditor.Pins;

namespace RE.Editor.NodeEditor.Nodes
{
    internal class TextNode : DataNode
    {
        public override string Name => "Text";

        private string buffer = "";

        DataNodePin<string> _output;

        public TextNode()
        {
            //_output = new DataNodePin<string>("text", this);
            OutputPins.Add(_output);
        }

        public override void RenderNode()
        {
            MinInputWidth = 200;
            base.RenderNode();
            ImGui.InputTextMultiline("##" + Id, ref buffer, 2048, new Vector2(200, 200));
        }
        
       // public override void Execute()
       // {
       //     _output.Value = buffer;
       // }
    }
}
