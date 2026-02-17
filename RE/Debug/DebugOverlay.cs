using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotRecast.Core.Collections;
using DotRecast.Core.Numerics;
using DotRecast.Detour;
using DotRecast.Recast;
using DotRecast.Recast.Geom;
using DotRecast.Recast.Toolset.Builder;
using Hexa.NET.ImGui;
using Hexa.NET.ImNodes;
using Hexa.NET.ImPlot;
using Microsoft.VisualBasic.Devices;
using OpenTK.Windowing.Common;
using RE.Audio;
using RE.Core;
using RE.Core.World;
using RE.Core.World.Components;
using RE.Core.World.Components.Physics;
using RE.Editor.NodeEditor;
using RE.Rendering;
using RE.Utils;
using static Hexa.NET.ImGui.ImGui;

namespace RE.Debug.Overlay;

internal class DebugOverlay : Renderable
{
    private DebugOverlay()
    {
#if DEBUG
        RenderManager.AddRenderable(this);
#endif
    }

    public static DebugOverlay? Instance { get; private set; }
    public override RenderLayer RenderLayer => RenderLayer.ImGui;
    public override bool IsVisible { get; set; } = true;

    private static readonly RingBuffer<int> Fps = new(1000);
    private static readonly RingBuffer<double> ManagedHeap = new(1000);
    private static readonly RingBuffer<double> PrivateBytes = new(1000);

    public override void Render(FrameEventArgs args)
    {
        RenderProfilersWindow(args);
        RenderTimingsWindowWhatTheHellIsThisHelpMe();
    }

    private void RenderTimingsWindowWhatTheHellIsThisHelpMe()
    {
        
    }

    private const float m_cellSize = 0.3f;
    private const float m_cellHeight = 0.2f;
    private const float m_agentHeight = 2.0f;
    private const float m_agentRadius = 0.6f;
    private const float m_agentMaxClimb = 0.9f;
    private const float m_agentMaxSlope = 45.0f;
    private const int m_regionMinSize = 8;
    private const int m_regionMergeSize = 20;
    private const float m_edgeMaxLen = 12.0f;
    private const float m_edgeMaxError = 1.3f;
    private const int m_vertsPerPoly = 6;
    private const float m_detailSampleDist = 6.0f;
    private const float m_detailSampleMaxError = 1.0f;
  
    public static void Init()
    {
        Instance ??= new DebugOverlay();
        Types = Assembly.GetExecutingAssembly().DefinedTypes
            .Where(s => s.IsAssignableTo(typeof(Node)) && !s.IsAbstract).Select(s => s.AsType()).ToList();
    }

    private static List<Type> Types = [];
    private static string _searchText = "";
    private static unsafe void RenderProfilersWindow(FrameEventArgs args)
    {
        UpdateUsageData();

        Fps.Add((int)(1 / args.Time));

        Begin("Stats");

        if (Button("crash"))
        {
            throw new Exception("Test exception for crash reporting");
        }

        var yMaxLimit = Game.FpsLock + Game.FpsLock * 0.1;
        var s = 150;
        var fps = Fps.ToArrayOrdered().Skip(Fps.Length - s).Select(s => (float)s).ToArray();
        fps[0] = 0;
        fps[^1] = 200;
        ImGui.PlotLines("FPS", ref fps[0], s, $"FPS: {fps[^2]}", new Vector2(s, 100));

        var r = PrivateBytes.ToArrayOrdered().Skip(Fps.Length - s).Select(s => (float)s).ToArray();
        r[0] = 0;
        r[^1] = 500;
        ImGui.PlotLines("RAM",
            ref r[0],
            s, $"RAM: {r[^2]:F1}Mb",
            new Vector2(s, 100));

        ImPlot.SetNextAxesLimits(0, Fps.Length + 1, 0, yMaxLimit);
        if (ImPlot.BeginPlot("#fps", ImPlotFlags.NoTitle | ImPlotFlags.NoLegend))
        {
            var xFlags = ImPlotAxisFlags.Invert | ImPlotAxisFlags.LockMin | ImPlotAxisFlags.LockMax;
            var yFlags = ImPlotAxisFlags.Opposite | ImPlotAxisFlags.LockMin | ImPlotAxisFlags.LockMax;

            ImPlot.SetupAxes($"Frames ago({((double)Fps.Length / Game.FpsLock):F1}s)", "Frames per second (1/dT)", xFlags, yFlags);

            fixed (int* a = Fps.ToArrayOrdered().Reverse().ToArray())
                ImPlot.PlotLine("#fps", a, Fps.Length);

            foreach (var (timestamp, e) in RenderProfiler.Events.Where(s => Time.ElapsedFrames - s.timestamp <= 1000))
            {
                ImPlot.PlotText(e, Time.ElapsedFrames - timestamp, yMaxLimit / 4, ImPlotTextFlags.Vertical);
            }

            ImPlot.EndPlot();
        }
        Text($"Mem: " + PrivateBytes.Last());
        yMaxLimit = (PrivateBytes.Last()) * 1.1;
        ImPlot.SetNextAxesLimits(0, ManagedHeap.Length + 1, 0, (PrivateBytes.Last()) * 1.1);
        if (ImPlot.BeginPlot("#mem", ImPlotFlags.NoTitle))
        {
            var xFlags = ImPlotAxisFlags.Invert | ImPlotAxisFlags.LockMin | ImPlotAxisFlags.LockMax;
            var yFlags = ImPlotAxisFlags.Opposite | ImPlotAxisFlags.LockMin | ImPlotAxisFlags.LockMax;

            ImPlot.SetupAxes($"Frames ago({((double)Fps.Length / Game.FpsLock):F1}s)", "MBytes", xFlags, yFlags);

            fixed (double* a = ManagedHeap.ToArrayOrdered().Reverse().ToArray())
                ImPlot.PlotLine("Managed Heap", a, ManagedHeap.Length);
            fixed (double* b = PrivateBytes.ToArrayOrdered().Reverse().ToArray())
                ImPlot.PlotLine("Working Set", b, PrivateBytes.Length);

            foreach (var (timestamp, e) in RenderProfiler.Events.Where(s => Time.ElapsedFrames - s.timestamp <= 1000))
            {
                ImPlot.PlotText(e, Time.ElapsedFrames - timestamp, yMaxLimit / 4, ImPlotTextFlags.Vertical);
            }

            ImPlot.EndPlot();
        }

        End();
    }

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
        }

        ImNodes.BeginNodeEditor();
        NodeManager.RenderNodes();
        NodeManager.RenderNodeLinks();
        ImNodes.EndNodeEditor();
        NodeManager.ResolveInput();
        End();
    }

    static void UpdateUsageData()
    {
        var managedHeap = GC.GetTotalMemory(false) / (1024.0 * 1024.0);

        WinApi.GetProcessMemoryInfo(Process.GetCurrentProcess().Handle, out var counters, (uint)Marshal.SizeOf<PROCESS_MEMORY_COUNTERS>());
        var workingSet = counters.WorkingSetSize / (1024.0 * 1024.0);

        ManagedHeap.Add(managedHeap);
        PrivateBytes.Add(workingSet);
    }
}