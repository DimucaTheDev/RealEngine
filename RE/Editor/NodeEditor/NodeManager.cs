using System.Text;
using Hexa.NET.ImGui;
using Hexa.NET.ImNodes;
using OpenTK.Mathematics;
using RE.Core;
using RE.Editor.NodeEditor.Nodes;
using RE.Editor.NodeEditor.Pins;
using RE.Utils;

namespace RE.Editor.NodeEditor
{
    internal class NodeManager
    {
        private static Dictionary<int, Node> Nodes = [];
        private static Dictionary<int, NodePin> NodePins = [];
        private static List<NodePinLink> NodeLinks = [];
        private static int _nextId;

        public static void RenderNodes()
        {
            //todo: make non static
            foreach (var node in Nodes)
            {
                var anyError = node.Value.Errors.Any();
                if (anyError)
                {
                    var c = Color4.Red with { A = MathF.Abs(MathF.Sin(Time.ElapsedTime * 2)) };
                    ImNodes.PushColorStyle(ImNodesCol.NodeOutline, c.ToImGuiColor());
                }
                node.Value.Errors.Clear();

                ImNodes.PushColorStyle(ImNodesCol.TitleBar, node.Value.NodeColor.ToImGuiColor());
                ImNodes.PushColorStyle(ImNodesCol.TitleBarHovered, Darken(node.Value.NodeColor.ToImGuiColor(), 0.7f));
                ImNodes.PushColorStyle(ImNodesCol.TitleBarSelected, Darken(node.Value.NodeColor.ToImGuiColor(), 0.6f));
                ImNodes.BeginNode(node.Value.Id);

                node.Value.RenderNode();

                ImNodes.EndNode();

                if (anyError)
                    ImNodes.PopColorStyle();

                ImNodes.PopColorStyle();
                ImNodes.PopColorStyle();
                ImNodes.PopColorStyle();
            }
        }
        public static void RenderNodeLinks()
        {
            //todo: make non static
            foreach (var link in NodeLinks)
            {
                var type = NodePins.First(s => s.Key == link.StartPinId).Value.ValueType;
                uint baseColor = NodePin.PinColors.TryGetValue(type, out var value)
                    ? value
                    : Color4.DarkGray.ToImGuiColor();

                ImNodes.PushColorStyle(ImNodesCol.Link, baseColor);
                ImNodes.PushColorStyle(ImNodesCol.LinkHovered, Darken(baseColor, 0.7f));
                ImNodes.PushColorStyle(ImNodesCol.LinkSelected, Darken(baseColor, 0.6f));


                ImNodes.Link(link.Id, link.StartPinId, link.EndPinId);

                ImNodes.PopColorStyle();
                ImNodes.PopColorStyle();
                ImNodes.PopColorStyle();
            }

        }

        public static int ObtainNodeId(Node node)
        {
            Nodes.Add(++_nextId, node);
            return _nextId;
        }

        public static int ObtainNodePinId(NodePin nodePin)
        {
            NodePins.Add(++_nextId, nodePin);
            return _nextId;
        }
        public static int ObtainNodeLinkId(NodePinLink nodePinLink)
        {
            nodePinLink.Id = ++_nextId;
            NodeLinks.Add(nodePinLink);
            return _nextId;
        }

        public static IEnumerable<NodePin> GetLinkedPins(NodePin nodePin)
        {
            var linkedIds = NodeLinks
                .Where(l => l.StartPinId == nodePin.Id || l.EndPinId == nodePin.Id)
                .Select(l => l.StartPinId == nodePin.Id ? l.EndPinId : l.StartPinId);

            foreach (var id in linkedIds)
            {
                if (NodePins.TryGetValue(id, out var linkedPin))
                {
                    yield return linkedPin;
                }
            }
        }

        public static void ResolveInput()
        {
            int hovNodeId = 0;
            if (ImNodes.IsNodeHovered(ref hovNodeId))
            {
                StringBuilder b = new();
                foreach (var error in Nodes[hovNodeId].Errors)
                {
                    if (b.Length > 0)
                        b.Append("\n");
                    b.Append($"{error.Key.Name}: {error.Value}");
                }
                if (b.Length > 0)
                    ImGui.SetTooltip(b.ToString());
            }

            int nodePinKey = 0;
            if (ImNodes.IsPinHovered(ref nodePinKey))
            {
                ImGui.SetTooltip($"Name: {NodePins[nodePinKey].Name}\nType: {NodePins[nodePinKey].ValueType.Name}\nMultiple Inputs: {NodePins[nodePinKey].MultipleInputs}");
            }


            int s = 0, e = 0, i = 0;
            if (ImNodes.IsLinkCreated(ref s, ref e))
            {
                if (!NodePins[e].MultipleInputs)
                    NodeLinks.RemoveAll(s => s.EndPinId == e);
                ObtainNodeLinkId(new NodePinLink { StartPinId = s, EndPinId = e });
            }
            if (ImNodes.IsLinkDestroyed(ref i))
            {
                NodeLinks.RemoveAll(s => s.Id == i);
            }
            if (ImGui.IsKeyReleased(ImGuiKey.Delete))
            {
                int numSelectedLinks = ImNodes.NumSelectedLinks();

                if (numSelectedLinks > 0)
                {
                    int[] selectedLinkIds = new int[numSelectedLinks];
                    ImNodes.GetSelectedLinks(ref selectedLinkIds[0]);

                    foreach (int linkId in selectedLinkIds)
                    {
                        NodeLinks.RemoveAll(s => s.Id == linkId);
                    }

                    ImNodes.ClearLinkSelection();
                }

                int numSelectedNodes = ImNodes.NumSelectedNodes();
                if (numSelectedNodes > 0)
                {
                    int[] selectedNodeIds = new int[numSelectedNodes];
                    ImNodes.GetSelectedNodes(ref selectedNodeIds[0]);

                    foreach (int nodeId in selectedNodeIds)
                    {
                        Nodes.Remove(nodeId);
                        NodeLinks.RemoveAll(s => NodePins[s.EndPinId].Owner.Id == nodeId || NodePins[s.StartPinId].Owner.Id == nodeId);
                    }
                    ImNodes.ClearNodeSelection();
                }
            }
        }

        static uint Darken(uint color, float factor)
        {
            factor = Math.Clamp(factor, 0f, 1f);

            byte r = (byte)(color & 0xFF);
            byte g = (byte)((color >> 8) & 0xFF);
            byte b = (byte)((color >> 16) & 0xFF);
            byte a = (byte)((color >> 24) & 0xFF);

            r = (byte)(r * factor);
            g = (byte)(g * factor);
            b = (byte)(b * factor);

            return (uint)(r | (g << 8) | (b << 16) | (a << 24));
        }

        public static void ExecuteNode(Node node, int? targetPinId = null)
        {
            if (node is WaitForAllNode barrier)
            {
                if (targetPinId.HasValue)
                {
                    if (!barrier.RegisterSignal(targetPinId.Value))
                    {
                        return;
                    }
                }
            }

            foreach (var input in node.InputPins.Where(p => p is not ActionNodePin))
            {
                input.Reset();
                var links = input.GetLinkedPins();

                if (links.Count() > 0)
                {

                    foreach (var link in links)
                    {
                        var sourceNode = link.Owner;
                        if (!sourceNode.IsEvaluated)
                            ExecuteNode(sourceNode);

                        input.TakeValueFrom(link);
                    }
                }
                else
                {
                    input.CopyLocalValue();
                }

                input.FinalizeValue();
            }

            node.Execute();
            node.IsEvaluated = true;

            if (node is ActionNode)
            {
                if(!node.OutputPins.Any()) return;
                
                var links = node.OutputPins.First().GetLinkedPins();
                foreach (var link in links)
                {
                    var nextNode = link.Owner;

                    ExecuteNode(nextNode, link.Id);
                }
            }
        }

        public static void ExecutePin(ActionNodePin truePin)
        {
            foreach (var pin in truePin.GetLinkedPins())
            {
                ExecuteNode(pin.Owner);
            }
        }

        public static void ResetAll()
        {
            foreach (var (_, value) in Nodes)
            {
                value.IsEvaluated = false;
                value.OutputPins.ForEach(s => s.Reset());
                value.InputPins.ForEach(s => s.Reset());
            }
        }

        public static Node FindNodeByPin(NodePin nodePin)
        {
            return Nodes.First(s =>
                    s.Value.OutputPins.Contains(nodePin) || s.Value.InputPins.Contains(nodePin)
                ).Value;
        }
    }
}
