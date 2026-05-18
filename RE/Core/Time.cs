using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using Serilog;

namespace RE.Core;

/// <summary>
/// Time is a static class that provides time-related functionality, such as scheduling tasks to be executed after a certain time span.
/// </summary>
public static class Time
{
    public class ScheduledTask(double targetTime, Action action)
    {
        public Action Action { get; } = action;
        public double TargetTime { get; } = targetTime;
    }

    private static readonly List<ScheduledTask> _scheduled = new();
    private static readonly List<(Func<float, bool> check, Action<float> action)> _updateUntil = new();

    /// <summary>
    /// Represents the time when the application has started.
    /// </summary>
    public static DateTime StartTime { get; private set; } = DateTime.Now;

    /// <summary>
    /// Represents the time when the last <see cref="GameWindow.UpdateFrame"/> occurred.
    /// </summary>
    public static DateTime LastUpdate { get; private set; } = StartTime;

    /// <summary>
    /// Time spent from last <see cref="GameWindow.UpdateFrame"/> call.
    /// </summary>
    public static float DeltaTime { get; private set; }

    /// <summary>
    /// Time elapsed since <see cref="StartTime"/> in seconds.
    /// </summary>
    public static float ElapsedTime => (float)(DateTime.Now - StartTime).TotalSeconds;

    /// <summary>
    /// Total frames elapsed since <see cref="StartTime"/>.
    /// </summary>
    public static long ElapsedFrames { get; private set; }

    /// <summary>
    /// Checks whether the specified <see cref="ScheduledTask"/> is currently scheduled.
    /// </summary>
    /// <param name="task"><see cref="ScheduledTask"/> to be checked</param>
    /// <returns><see langword="true"/> if task is scheduled, <see langword="false"/> if not</returns>
    public static bool IsScheduled(this ScheduledTask task) => _scheduled.Contains(task);

    /// <summary>
    /// Schedules the specified action to be executed after the given time span.
    /// </summary>
    /// <param name="span">Time span after action will be executed</param>
    /// <param name="action">Action that will be executed after time span</param>
    /// <returns>New scheduled <see cref="ScheduledTask"/> instance</returns>
    public static ScheduledTask Schedule(TimeSpan span, Action action) => Schedule((int)span.TotalMilliseconds, action);

    ///<inheritdoc cref="Schedule(TimeSpan, Action)"/>
    /// <param name="ms">Time measured in milliseconds after action will be executed</param>
    public static ScheduledTask Schedule(int ms, Action action)
    {
        var scheduledTask = new ScheduledTask(ElapsedTime + (double)ms / 1000, action);
        _scheduled.Add(scheduledTask);
        return scheduledTask;
    }

    /// <summary>
    /// Removes the specified scheduled task if it is currently scheduled.
    /// </summary>
    /// <remarks>
    /// The task will not be executed if it is successfully removed.
    /// </remarks>
    /// <param name="task"><see cref="ScheduledTask"/> instance to remove.</param>
    public static void RemoveTask(ScheduledTask task)
    {
        if (_scheduled.Contains(task))
            _scheduled.Remove(task);
        else
            Log.Warning("Tried to remove a task that was not scheduled: {MethodName}", task.Action.Method.Name);
    }

    public static void Update(FrameEventArgs args)
    {
        LastUpdate = DateTime.Now;
        DeltaTime = (float)args.Time;
        ElapsedFrames++;
        for (var i = _scheduled.Count - 1; i >= 0; i--)
        {
            if (ElapsedTime >= _scheduled[i].TargetTime)
            {
                _scheduled[i].Action();
                _scheduled.RemoveAt(i);
            }
        }
    }
}