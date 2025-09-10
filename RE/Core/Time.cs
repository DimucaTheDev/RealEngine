using OpenTK.Windowing.Common;
using Serilog;

namespace RE.Core;

public static class Time
{
    public class ScheduledTask(double targetTime, Action action)
    {
        public Action Action { get; } = action;
        public double TargetTime { get; } = targetTime;
    }

    private static bool _initialized;
    private static readonly List<ScheduledTask> _scheduled = new();
    private static readonly List<ScheduledTask> _scheduledFrames = new();
    private static List<(Func<float, bool> check, Action<float> action)> updateUntil = new();

    public static DateTime StartTime { get; private set; } = DateTime.Now;
    public static DateTime LastUpdate { get; private set; } = StartTime;
    public static float DeltaTime { get; private set; }
    public static float ElapsedTime => (float)(DateTime.Now - StartTime).TotalSeconds;
    public static long ElapsedFrames { get; private set; }
    public static TimeSpan TotalTime => DateTime.Now - StartTime;

    public static bool IsScheduled(this ScheduledTask task) => _scheduled.Contains(task) || _scheduledFrames.Contains(task);

    public static ScheduledTask Schedule(TimeSpan span, Action action) => Schedule((int)span.TotalMilliseconds, action);
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
            Log.Warning("Tried to remove a task that was not scheduled: {MethodName}", task.Action.Method.Name);
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
            Log.Warning("Tried to init {Name} again!", nameof(Time));
            return;
        }

        Game.Instance.UpdateFrame += Update;
        StartTime = DateTime.Now;
        LastUpdate = StartTime;
        _initialized = true;
    }

    public static void Update(FrameEventArgs args)
    {
        if (!_initialized)
            Init();
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
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[Timer] Exception: {Exception}", ex);
                }
                finally
                {
                    _scheduled.RemoveAt(i);
                }
            }

        for (int i = updateUntil.Count - 1; i >= 0; i--)
        {
            var (predicate, action) = updateUntil[i];
            action((float)args.Time);
            if (predicate((float)args.Time))
                updateUntil.RemoveAt(i);
        }
    }

    public static void OnUpdateUntil(Func<float, bool> predicate, Action<float> action)
    {
        updateUntil.Add((predicate, action));
    }
}