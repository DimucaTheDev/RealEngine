using OpenTK.Windowing.Common;
using Serilog;

namespace RE.Core;

public static class Time
{
    private static bool _initialized;
    private static readonly List<ScheduledTask> _scheduled = new();
    private static readonly List<ScheduledTask> _scheduledFrames = new();

    public static DateTime StartTime { get; private set; } = DateTime.Now;
    public static DateTime LastUpdate { get; private set; } = StartTime;
    public static float DeltaTime { get; private set; }
    public static float ElapsedTime => (float)(DateTime.Now - StartTime).TotalSeconds;
    public static long ElapsedFrames { get; private set; }
    public static TimeSpan TotalTime => DateTime.Now - StartTime;

    public static bool IsScheduled(this ScheduledTask task) => _scheduled.Contains(task) || _scheduledFrames.Contains(task);
    public static ScheduledTask Schedule(int ms, Action action)
    {
        var scheduledTask = new ScheduledTask(ElapsedTime + (double)ms / 1000, action);
        _scheduled.Add(scheduledTask);
        return scheduledTask;
    }
    public static void RemoveTask(ScheduledTask task)
    {
        if (_scheduled.Contains(task))
            _scheduled.Remove(task);
        else if (_scheduledFrames.Contains(task))
            _scheduledFrames.Remove(task);
        else
            Log.Warning($"Tried to remove a task that was not scheduled: {task.Action.Method.Name}");
    }

    [Obsolete("Doesnt work!!!!", true)]
    public static void ScheduleFrames(int frames, Action action)
    {
        _scheduledFrames.Add(new ScheduledTask(ElapsedFrames + frames, action));
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
        Game.Instance.Title = (1 / DeltaTime).ToString("F2");
        for (var i = _scheduled.Count - 1; i >= 0; i--)
            if (ElapsedTime >= _scheduled[i].TargetTime)
            {
                try
                {
                    _scheduled[i].Action?.Invoke();
                    _scheduled.RemoveAt(i);
                }
                catch (Exception ex)
                {
                    Log.Error($"[Timer] Exception: {ex}");
                }
            }
    }

    public class ScheduledTask(double targetTime, Action action)
    {
        public Action Action { get; } = action;
        public double TargetTime { get; } = targetTime;
    }
}