using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using Serilog;

namespace RE.Core.Initializing
{
    public sealed class Initializer
    {
        public static bool HasJob => _queue.Count > 0 || _runningTask != null;

        private static readonly Queue<InitializingTask> _queue = new();
        private static Task? _runningTask;
        private static string? _runningTaskName;
        private static bool _initialized;

        public static void AddStep(InitializingTask step) => _queue.Enqueue(step);

        private static void UpdateScreen()
        {
            if (!_initialized)
            {

                _initialized = true;
            }
            GL.ClearColor(0.1f, 0.1f, 0.1f, 1f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            Game.Instance.SwapBuffers();
        }

        public static bool Render(FrameEventArgs args)
        {
            if (_runningTask != null)
            {
                if (!_runningTask.IsCompleted)
                {
                    UpdateScreen();
                    return true;
                }

                if (_runningTask.IsFaulted)
                {
                    Log.Error(_runningTask.Exception.GetBaseException(),
                        "Exception during task {TaskName} occurred.",
                        _runningTaskName);
                }

                _runningTask = null;
            }

            if (!_queue.TryDequeue(out var task))
            {
                return false;
            }

            UpdateScreen();

            Log.Debug("Executing {TaskName}", task.Label ?? "Unnamed task");
            _runningTaskName = task.Label ?? "Unnamed Task";

            switch (task)
            {
                case SyncInitializingTask syncTask:
                    syncTask.Action.Invoke();
                    return true;
                case AsyncInitializingTask asyncTask:
                    _runningTask = Task.Run(() => asyncTask.Action.Invoke());
                    return true;
                default:
                    return HasJob;
            }
        }
    }
}