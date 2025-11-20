using System.Numerics;
using Hexa.NET.ImPlot;
using OpenTK.Windowing.Common;
using RE.Core;
using RE.Rendering;
using static Hexa.NET.ImGui.ImGui;

namespace RE.Debug.Overlay;

internal class DebugOverlay : Renderable
{

    private DebugOverlay()
    {
        RenderManager.AddRenderable(this);
    }

    public static DebugOverlay? Instance { get; private set; }
    public override RenderLayer RenderLayer => RenderLayer.ImGui;
    public override bool IsVisible { get; set; } = true;


    private static RingBuffer<int> Fps = new(1000);


    public override void Render(FrameEventArgs args) { RenderProfilersWindow(args); }
    public static void Init()
    {
        Instance ??= new DebugOverlay();
    }


    private static unsafe void RenderProfilersWindow(FrameEventArgs args)
    {
        Fps.Add((int)(1 / args.Time));


        Begin("##profilers");

        double yMaxLimit = Game.FpsLock + Game.FpsLock * 0.1;

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


        End();
    }

    static void UpdateUsageData()
    {

    }
}
class RingBuffer<T>
{
    private readonly T[] _data;
    private int _index;

    public RingBuffer(int size)
    {
        _data = new T[size];
        _index = 0;
    }

    public int Length => _data.Length;

    public void Add(T item)
    {
        _data[_index] = item;
        _index = (_index + 1) % _data.Length;
    }
    public T[] ToArrayOrdered()
    {
        var res = new T[_data.Length];
        int p = 0;

        for (int i = _index; i < _data.Length; i++)
            res[p++] = _data[i];

        for (int i = 0; i < _index; i++)
            res[p++] = _data[i];
        return res;
    }

    public T[] GetAll() => _data;
}
