using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Rendering.Renderables;
using RE.Rendering.Text;
using RE.Utils;
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
        private static ScreenText _text;
        private static ImageRenderer _image;

        private static readonly string[] _dots = ["", ".", "..", "..."];

        public static void AddStep(InitializingTask step) => _queue.Enqueue(step);

        private static void UpdateScreen()
        {
            if (!_initialized)
            {
                _text = new ScreenText(null, new(Game.Instance.ClientSize.X / 2, Game.Instance.ClientSize.Y / 4 * 3), (FreeTypeFont)Fonts.Default, 1);
                _text.Color = Vector4.One;
                _image = new ImageRenderer("Assets/Splash.png", new Vector2(Game.Instance.ClientSize.X / 2 - (400*1.75f)/2, Game.Instance.ClientSize.Y / 2 - 230), new Vector2(400 * 1.75f, 100 * 1.75f));
                _initialized = true;
            }
            GL.ClearColor(0.1f, 0.1f, 0.1f, 1f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            _text.Text = _runningTaskName + (_runningTask != null ? (_dots[((int)(Time.ElapsedTime * 2)) % 4]) : "");
            _text.Position = _text.Position with
            {
                X = Game.Instance.ClientSize.X / 2 - _text.Font.GetTextWidth(_text.Text) / 2
            };
            _text.Render(new FrameEventArgs());
            _image.Render(new FrameEventArgs());
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

            _runningTaskName = task.Label ?? "Unnamed Task";
            UpdateScreen();

            Log.Debug("Executing {TaskName}", task.Label ?? "Unnamed task");

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