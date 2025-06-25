using OpenTK.Windowing.Common;
using Serilog;

namespace RE.Core;

internal static class Time
{
    private const double interval = 5.0;

    private static bool _initialized;
    private static readonly List<ScheduledTask> _scheduled = new();
    private static readonly List<ScheduledTask> _scheduledFrames = new();
    private static double accumTime = 0.0;
    private static int frameCount = 0;
    private static double avgFPS = 0.0;

    public static DateTime StartTime { get; private set; } = DateTime.Now;
    public static DateTime LastUpdate { get; private set; } = StartTime;
    public static float DeltaTime { get; private set; }
    public static float ElapsedTime => (float)(DateTime.Now - StartTime).TotalSeconds;
    public static long ElapsedFrames { get; private set; }
    public static TimeSpan TotalTime => DateTime.Now - StartTime;

    public static void Schedule(int ms, Action action)
    {
        _scheduled.Add(new ScheduledTask
        {
            TargetTime = ElapsedTime + (double)ms / 1000,
            Action = action
        });
    }

    [Obsolete("Doesnt work!!!!", true)]
    public static void ScheduleFrames(int frames, Action action)
    {
        _scheduledFrames.Add(new ScheduledTask
        {
            TargetTime = ElapsedFrames + frames,
            Action = action
        });
    }

    public static void Init()
    {
        if (_initialized)
        {
            Log.Warning($"Tried to init {nameof(Time)} again!");
            return;
        }

        StartTime = DateTime.Now;
        LastUpdate = StartTime;
        _initialized = true;
    }

    public static void Update(FrameEventArgs args)
    {
        if (!_initialized) Init();
        LastUpdate = DateTime.Now;
        DeltaTime = (float)args.Time;
        ElapsedFrames++;

        //accumTime += args.Time;
        //frameCount++;

        //if (accumTime >= interval)
        //{
        //    avgFPS = frameCount / accumTime;
        //    accumTime = 0.0;
        //    frameCount = 0;
        //    Game.A = avgFPS;
        //}

        for (var i = _scheduled.Count - 1; i >= 0; i--)
            if (ElapsedTime >= _scheduled[i].TargetTime)
            {
                try
                {
                    _scheduled[i].Action?.Invoke();
                }
                catch (Exception ex)
                {
                    Log.Error($"[Timer] Exception: {ex}");
                }

                _scheduled.RemoveAt(i);
            }
    }

    private class ScheduledTask
    {
        public Action Action;
        public double TargetTime;
    }
}