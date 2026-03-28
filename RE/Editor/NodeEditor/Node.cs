using Hexa.NET.ImGui;
using Hexa.NET.ImNodes;
using OpenTK.Mathematics;
using RE.Core;
using RE.Editor.NodeEditor.Pins;
using RE.Utils;
using Serilog.Events;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace RE.Editor.NodeEditor
{
    public abstract class Node
    {
        protected Node()
        {
            Id = NodeManager.ObtainNodeId(this);
        }

        public int Id { get; set; }
        public bool IsEvaluated { get; internal set; }
        public List<NodePin> InputPins { get; } = [];
        public List<NodePin> OutputPins { get; } = [];
        public Dictionary<NodePin, string> Errors { get; protected set; } = [];
        public Color4 NodeColor { get; set; } = Color4.FromHsv(new OpenTK.Mathematics.Vector4(Random.Shared.NextSingle(), 0.8f, 0.9f, 1));

        public Action<Node> ExecuteAction;

        public virtual string Name { get; set; }
        protected float MinInputWidth;

        public void Execute() => ExecuteAction?.Invoke(this);

        public virtual void RenderNode()
        {
            ImNodes.BeginNodeTitleBar();
            ImGui.Text(Name);
            ImNodes.EndNodeTitleBar();

            foreach (var pin in InputPins)
                MinInputWidth = Math.Max(MinInputWidth, pin.GetRequiredWidth()) + ImGui.CalcTextSize(IconFont.ExclamationTriangle).X;

            float minOutputWidth = 0f;
            foreach (var pin in OutputPins)
                minOutputWidth = Math.Max(minOutputWidth, pin.GetRequiredWidth()) + ImGui.CalcTextSize(IconFont.ExclamationTriangle).X;

            var style = ImGui.GetStyle();
            float nodeWidth =
                MinInputWidth +
                10 +
                minOutputWidth +
                style.CellPadding.X * 4;

            if (ImGui.BeginTable($"node_table_{Id}", 3, ImGuiTableFlags.SizingFixedFit, new Vector2(nodeWidth, 0)))
            {
                ImGui.TableSetupColumn("Inputs", ImGuiTableColumnFlags.WidthFixed, MinInputWidth);
                ImGui.TableSetupColumn("Dummy", ImGuiTableColumnFlags.WidthFixed, 0);
                ImGui.TableSetupColumn("Outputs", ImGuiTableColumnFlags.WidthFixed, minOutputWidth);
                int rowCount = Math.Max(InputPins.Count, OutputPins.Count);

                for (int i = 0; i < rowCount; i++)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();

                    if (i < InputPins.Count)
                    {
                        var inputPin = InputPins[i];
                        ImNodes.PushColorStyle(ImNodesCol.Pin, GetPinColor(inputPin));
                        ImNodes.PushColorStyle(ImNodesCol.PinHovered, Darken(GetPinColor(inputPin), 0.7f));

                        var inputPinId = inputPin.Id;
                        ImNodes.BeginInputAttribute(inputPinId);
                        inputPin.RenderPin();
                        if (NodeManager.GetLinkedPins(inputPin).Any(s => s.ValueType != typeof(void) && s.ValueType.MakeArrayType() != inputPin.ValueType && !s.ValueType.IsAssignableTo(inputPin.ValueType)))
                        {
                            ImGui.SameLine();
                            ImGui.TextColored(Color4.Gold.ToVec4Color() with { W = MathF.Sin(Time.ElapsedTime * 4) > 0 ? 1 : 0 }, IconFont.ExclamationTriangle);
                            Errors.Add(inputPin, $"Input Action ({inputPin.ValueType.Name}) != Output Action ({NodeManager.GetLinkedPins(inputPin).First(s => s.ValueType != inputPin.ValueType).ValueType.Name})");
                        }

                        if (!inputPin.GetLinkedPins().Any())
                        {
                            DrawWidget(inputPin);
                        }

                        ImNodes.EndInputAttribute();

                        ImNodes.PopColorStyle();
                        ImNodes.PopColorStyle();
                    }

                    ImGui.TableNextColumn();
                    ImGui.Dummy(new Vector2(10, 0));

                    ImGui.TableNextColumn();
                    if (i < OutputPins.Count)
                    {
                        var outputPin = OutputPins[i];

                        ImNodes.PushColorStyle(ImNodesCol.Pin, GetPinColor(outputPin));
                        ImNodes.PushColorStyle(ImNodesCol.PinHovered, Darken(GetPinColor(outputPin), 0.7f));

                        int col = ImGui.TableGetColumnIndex();
                        float pinWidth = outputPin.GetRequiredWidth();
                        float colWidth = ImGui.GetColumnWidth(col);
                        float padding = style.CellPadding.X;
                        float shift = colWidth - pinWidth - padding;

                        if (shift > 0f)
                            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + shift);

                        var outputPinId = outputPin.Id;
                        ImNodes.BeginOutputAttribute(outputPinId);
                        outputPin.RenderPin();
                        ImNodes.EndOutputAttribute();

                        ImNodes.PopColorStyle();
                        ImNodes.PopColorStyle();
                    }
                }
                ImGui.EndTable();
            }
            MinInputWidth = 0f;
        }

        private static void DrawWidget(NodePin pin)
        {
            switch (pin)
            {
                case ActionNodePin a:
                    // No widget for action pins
                    break;
                case DataNodePin<int> i:
                    var iValue = i.LocalValue;
                    if (ImGui.DragInt("##" + pin.Id, ref iValue))
                        i.LocalValue = iValue;
                    break;
                case DataNodePin<float> i:
                    var fValue = i.LocalValue;
                    if (ImGui.DragFloat("##" + pin.Id, ref fValue))
                        i.LocalValue = fValue;
                    break;
                case DataNodePin<bool> i:
                    var bValue = i.LocalValue;
                    if (ImGui.Checkbox("##" + pin.Id, ref bValue))
                        i.LocalValue = bValue;
                    break;
                case DataNodePin<string> s:
                    var sLocalValue = s.LocalValue ?? "";
                    ImGui.SetNextItemWidth(-1);
                    ImGui.InputText("##" + s.Id, ref sLocalValue, 512);
                    s.LocalValue = sLocalValue;
                    break;
                case DataNodePin<LogEventLevel> f:
                    string[] names = Enum.GetNames(typeof(LogEventLevel));
                    var values = (LogEventLevel[])Enum.GetValues(typeof(LogEventLevel));

                    int currentIndex = Array.IndexOf(values, f.LocalValue);
                    if (currentIndex == -1)
                        currentIndex = 0;

                    ImGui.PushItemWidth(120);

                    if (ImGui.Combo($"##{pin.Id}", ref currentIndex, names, names.Length))
                    {
                        f.LocalValue = values[currentIndex];
                    }
                    ImGui.PopItemWidth();
                    break;
                default:
                    ImGui.Text($"No widget for type {pin.ValueType.Name}");
                    break;
            }
            ImGui.SameLine();
        }

        private uint GetPinColor(NodePin pin)
        {
            return NodePin.PinColors.TryGetValue(pin.ValueType, out var color)
                ? color
                : ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0.2f));
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
    }

    public class DataNode : Node
    { 
    }

    internal class ActionNode : Node
    {
    }
}
