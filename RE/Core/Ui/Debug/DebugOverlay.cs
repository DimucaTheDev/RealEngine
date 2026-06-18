using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using Hexa.NET.ImGui;
using Hexa.NET.ImNodes;
using Hexa.NET.ImPlot;
using OpenTK.Windowing.Common;
using RE.Core.Audio;
using RE.Core.World;
using RE.Core.World.Physics;
using RE.Editor.NodeEditor;
using RE.Rendering;
using RE.Utils;
using static Hexa.NET.ImGui.ImGui;
using Game = RE.Utils.Game;

namespace RE.Core.Ui.Debug;

internal class DebugOverlay : Renderable
{
    private DebugOverlay()
    {
        RenderManager.AddRenderable(this);
    }

    public static DebugOverlay? Instance { get; private set; }
    public override bool IsVisible { get; set; } = true;

    private static readonly RingBuffer<int> Fps = new(1000);
    private static readonly RingBuffer<double> ManagedHeap = new(1000);
    private static readonly RingBuffer<double> PrivateBytes = new(1000);
    private static int _updFrames = 1;
    private static int _timeTreshold;

    public override void Render(FrameEventArgs args)
    {
        RenderTimingsWindowWhatTheHellIsThisHelpMe();
        Test();
    }

    private string? save = null;

    private void Test()
    {
        Begin("load save test");
        if (Button("save"))
        {
            save = SceneSerializer.SerializeScene(SceneManager.CurrentScene);
        }

        if (save != null && Button("load"))
        {
            var scene = SceneSerializer.DeserializeScene(save);
        }

        End();
    }

    private void RenderTimingsWindowWhatTheHellIsThisHelpMe()
    {
        Begin("Profiler Window");

        var root = FrameProfiler.GetLastFrame();
        if (root.End <= 0)
        {
            End();
            return;
        }

        SetNextItemWidth(200);
        if (SliderInt("Update info every N frames", ref _updFrames, 1,
                Math.Max(1, 2 * (int)Game.Instance.UpdateFrequency)))
            FrameProfiler.UpdateDelay = Math.Max(1, _updFrames);
        SetNextItemWidth(200);
        SliderInt("Do not show nodes with ns below", ref _timeTreshold, 0, 100);

        Separator();

        float rowHeight = 22f;
        float minWidthToDrawText = 60f;
        var origin = GetCursorScreenPos();
        var draw = GetWindowDrawList();
        float width = GetContentRegionAvail().X;
        double totalTime = root.End - root.Start;

        uint GetNodeColor(string name, int depth)
        {
            int hash = name.GetHashCode();

            float hue = (hash & 0xFFFF) / (float)0xFFFF;

            float sat = 0.5f + ((hash >> 16 & 0xFF) / 255f) * 0.2f;

            float light = Math.Clamp(0.7f - depth * 0.03f, 0.2f, .7f);

            Vector3 HsvToRgb(float h, float s, float v)
            {
                float r = 0, g = 0, b = 0;
                int i = (int)(h * 6);
                float f = h * 6 - i;
                float p = v * (1 - s);
                float q = v * (1 - f * s);
                float t = v * (1 - (1 - f) * s);

                switch (i % 6)
                {
                    case 0:
                        r = v;
                        g = t;
                        b = p;
                        break;
                    case 1:
                        r = q;
                        g = v;
                        b = p;
                        break;
                    case 2:
                        r = p;
                        g = v;
                        b = t;
                        break;
                    case 3:
                        r = p;
                        g = q;
                        b = v;
                        break;
                    case 4:
                        r = t;
                        g = p;
                        b = v;
                        break;
                    case 5:
                        r = v;
                        g = p;
                        b = q;
                        break;
                }

                return new Vector3(r, g, b);
            }

            Vector3 rgb = HsvToRgb(hue, sat, light);
            return ColorConvertFloat4ToU32(new Vector4(rgb, 1f));
        }


        void DrawNode(FrameProfiler.ProfilerNode node, int depth)
        {
            if (_timeTreshold / 1000d >= (node.End - node.Start))
                return;

            float x = origin.X + (float)(node.Start / totalTime * width);
            float w = (float)((node.End - node.Start) / totalTime * width);
            w = MathF.Max(w, 2f);
            float y = origin.Y + depth * rowHeight;

            uint col = GetNodeColor(node.Name, depth);

            draw.AddRectFilled(new Vector2(x, y), new Vector2(x + w, y + rowHeight - 2), col);

            string label = $"{node.Name} {(node.End - node.Start):0.00} ms";
            if (w >= CalcTextSize(label).X)
            {
                draw.AddText(new Vector2(x + 3, y + 3), 0xffffffff, label);
            }
            else
            {
                label = $"{(node.End - node.Start):0.00} ms";
                if (w >= CalcTextSize(label).X)
                {
                    draw.AddText(new Vector2(x + 3, y + 3), 0xffffffff, label);
                }
                else
                {
                    label = $"{(node.End - node.Start):0.##}";
                    if (w >= CalcTextSize(label).X)
                    {
                        draw.AddText(new Vector2(x + 3, y + 3), 0xffffffff, label);
                    }
                }
            }

            foreach (var child in node.Children)
                DrawNode(child, depth + 1);
        }

        DrawNode(root, 0);

        int maxDepth = GetDepth(root);
        Dummy(new Vector2(width, maxDepth * rowHeight));

        void TextNodeInfo(FrameProfiler.ProfilerNode node, string idPrefix = "")
        {
            string id = idPrefix + node.Name;

            ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanAvailWidth;

            string displayName = $"{node.Name} ({(node.End - node.Start):0.000} ms)";

            if (_timeTreshold / 1000d >= (node.End - node.Start))
                return;

            if (node.Children.Count == 0)
            {
                Text("  " + displayName);
                return;
            }

            if (TreeNodeEx(id + $"##{id}", flags, displayName))
            {
                int childIndex = 0;
                foreach (var child in node.Children)
                {
                    TextNodeInfo(child, id + "_" + childIndex);
                    childIndex++;
                }

                TreePop();
            }
        }

        BeginChild("##nodes", new Vector2(0, 300), ImGuiWindowFlags.None);

        TextNodeInfo(root);

        EndChild();

        Separator();

        Text($"est. fps: {(int)(1 / ((root.End - root.Start) / 1000))}");

        void UpdateHistory(FrameProfiler.ProfilerNode node)
        {
            if (Time.ElapsedFrames % _updFrames != 0)
                return;

            double value = node.End - node.Start;

            if (NodeFrameSum.ContainsKey(node.Name))
                NodeFrameSum[node.Name] += value;
            else
                NodeFrameSum[node.Name] = value;

            foreach (var child in node.Children)
                UpdateHistory(child);
        }

        void CommitFrameHistory()
        {
            foreach (var kv in NodeFrameSum)
            {
                if (!NodeHistory.TryGetValue(kv.Key, out var buffer))
                {
                    buffer = new RingBuffer<double>(100);
                    NodeHistory[kv.Key] = buffer;
                    NodeEnabled[kv.Key] = false;
                }

                buffer.Add(kv.Value);
            }

            NodeFrameSum.Clear();
        }

        UpdateHistory(root);
        CommitFrameHistory();

        if (CollapsingHeader("Node Graph"))
        {
            BeginChild("##checkboxes", new Vector2(0, 160), ImGuiWindowFlags.None);
            foreach (var key in NodeEnabled.Keys.ToList())
            {
                bool val = NodeEnabled[key];
                if (Checkbox(key, ref val))
                    NodeEnabled[key] = val;
            }

            EndChild();

            ImPlot.SetNextAxesLimits(0, NodeHistory.Values.First().Length, 0, NodeHistory.Values.Max(b => b.Last()));
            if (ImPlot.BeginPlot("Node Times"))
            {
                foreach (var kv in NodeHistory)
                {
                    if (!NodeEnabled[kv.Key])
                        continue;

                    var data = kv.Value.ToArrayOrdered();
                    ImPlot.PlotLine(kv.Key, ref data[0], data.Length);
                }

                ImPlot.EndPlot();
            }
        }

        End();
    }

    private static readonly Dictionary<string, RingBuffer<double>> NodeHistory = new();
    private static readonly Dictionary<string, bool> NodeEnabled = new();
    private static readonly Dictionary<string, double> NodeFrameSum = new();

    private static int GetDepth(FrameProfiler.ProfilerNode n)
    {
        int d = 1;
        foreach (var c in n.Children)
            d = Math.Max(d, 1 + GetDepth(c));
        return d;
    }

    public static void Init()
    {
        Instance ??= new DebugOverlay();
        Types = Assembly.GetExecutingAssembly().DefinedTypes
            .Where(s => s.IsAssignableTo(typeof(Node)) && !s.IsAbstract).Select(s => s.AsType()).ToList();
    }

    private static List<Type> Types = [];
    private static string _searchText = "";

    static void NodeTest()
    {
        InputText("Поиск ноды", ref _searchText, 100);

        var categories = Types
            .Where(t => string.IsNullOrEmpty(_searchText) ||
                        t.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
            .GroupBy(t => "Other");

        foreach (var category in categories)
        {
            Begin("Node test");
            if (CollapsingHeader(category.Key))
            {
                foreach (var type in category)
                {
                    string displayName = type.Name.Replace("Node", "");

                    if (Button(displayName))
                    {
                        var instance = Activator.CreateInstance(type);
                    }

                    if (GetItemRectMax().X < GetWindowPos().X + GetContentRegionAvail().X - 100)
                        SameLine();
                }

                NewLine();
            }

            End();
        }

        ImNodes.BeginNodeEditor();
        NodeManager.RenderNodes();
        NodeManager.RenderNodeLinks();
        ImNodes.EndNodeEditor();
        NodeManager.ResolveInput();
    }

    static void UpdateUsageData()
    {
        var managedHeap = GC.GetTotalMemory(false) / (1024.0 * 1024.0);

        WinApi.GetProcessMemoryInfo(Process.GetCurrentProcess().Handle, out var counters,
            (uint)Marshal.SizeOf<PROCESS_MEMORY_COUNTERS>());
        var workingSet = counters.WorkingSetSize / (1024.0 * 1024.0);

        ManagedHeap.Add(managedHeap);
        PrivateBytes.Add(workingSet);
    }
}