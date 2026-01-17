using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Hexa.NET.ImGui;
using Hexa.NET.ImNodes;
using Hexa.NET.ImPlot;
using Microsoft.VisualBasic.Devices;
using OpenTK.Windowing.Common;
using RE.Core;
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
    }

    public static void Init()
    {
        Instance ??= new DebugOverlay();
    }

    private static List<(int id, int start, int end)> myLinks = [];
    private static unsafe void RenderProfilersWindow(FrameEventArgs args)
    {
        UpdateUsageData();

        Fps.Add((int)(1 / args.Time));

        Begin("Stats");

        ImNodes.BeginNodeEditor();
        ImNodes.BeginNode(1); // ID ноды

        // Заголовок
        ImNodes.BeginNodeTitleBar();
        ImGui.TextUnformatted("Моя Нода");
        ImNodes.EndNodeTitleBar();

        // Входной пин (Input Pin)
        ImNodes.BeginInputAttribute(2); // ID пина
        ImGui.Text("Вход");
        ImNodes.EndInputAttribute();

        // Выходной пин (Output Pin)
        ImNodes.BeginOutputAttribute(3); // ID пина
        ImGui.Text("Выход");
        ImNodes.EndOutputAttribute();

        ImNodes.EndNode();

        ImNodes.BeginNode(4); // ID ноды

        // Заголовок
        ImNodes.BeginNodeTitleBar();
        ImGui.TextUnformatted("Моя Нода");
        ImNodes.EndNodeTitleBar();

        // Входной пин (Input Pin)
        ImNodes.BeginInputAttribute(5); // ID пина
        ImGui.Text("Вход");
        ImNodes.EndInputAttribute();
        bool ba = false;
        ImGui.TextColored(new Vector4(1, 1, 0, 1), IconFont.ExclamationTriangle);
        ImGui.SameLine();
        Checkbox("123", ref ba);
        Text("321");
        // Выходной пин (Output Pin)
        ImNodes.BeginOutputAttribute(6); // ID пина
        ImGui.Text("Выход");
        SameLine();
        Checkbox("321", ref ba);
        ImNodes.EndOutputAttribute();

        ImNodes.EndNode();
        foreach (var link in myLinks)
        {
            ImNodes.Link(link.id, link.start, link.end);
        }
        ImNodes.EndNodeEditor();
        int n = 0;
        if (ImNodes.IsNodeHovered(ref n))
        {
            var анрилСосать = IconFont.CheckCircle + "Анрил сосать";
            SetTooltip(анрилСосать);
        }

        if (ImGui.IsKeyPressed(ImGuiKey.Delete))
        {
            int numSelected = ImNodes.NumSelectedLinks();
            if (numSelected > 0)
            {
                int[] selectedLinkIds = new int[numSelected];
                ImNodes.GetSelectedLinks(ref selectedLinkIds[0]);

                foreach (int id in selectedLinkIds)
                {
                    myLinks.RemoveAll(l => l.id == id);
                }
            }
        }
        int startPin = 0, endPin = 0, dp = 0;
        if (ImNodes.IsLinkCreated(ref startPin, ref endPin))
        {
            myLinks.Add((myLinks.Count, startPin, endPin));
        }
        if (ImNodes.IsLinkDestroyed(ref dp))
        {
            myLinks.RemoveAll(s => s.id == dp);
        }


        var yMaxLimit = Game.FpsLock + Game.FpsLock * 0.1;
        var s = 150;
        var fps = Fps.ToArrayOrdered().Skip(Fps.Length - s).Select(s => (float)s).ToArray();
        ImGui.PlotLines("FPS", ref fps[0], s, $"FPS: {fps.Last()}", new Vector2(s, 100));


        ImGui.PlotLines("RAM",
            ref PrivateBytes.ToArrayOrdered().Skip(Fps.Length - s).Select(s => (float)s).ToArray()[0],
            s, $"RAM: {PrivateBytes.Last():F1}Mb",
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
        //todo: fix plot doesnt scale on Y axis
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

    static void UpdateUsageData()
    {
        var managedHeap = GC.GetTotalMemory(false) / (1024.0 * 1024.0);

        WinApi.GetProcessMemoryInfo(Process.GetCurrentProcess().Handle, out var counters, (uint)Marshal.SizeOf<PROCESS_MEMORY_COUNTERS>());
        var workingSet = counters.WorkingSetSize / (1024.0 * 1024.0);

        ManagedHeap.Add(managedHeap);
        PrivateBytes.Add(workingSet);
    }
}