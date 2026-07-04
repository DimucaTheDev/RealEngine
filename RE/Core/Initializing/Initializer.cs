using System.Diagnostics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Rendering.Renderables;
using RE.Rendering.Text;
using RE.Utils;
using Serilog;

namespace RE.Core.Initializing
{
    public static class Initializer
    {
        public static bool HasJob => _queue.Count > 0 || _runningTask != null || _finishAnimation;

        private static readonly Queue<InitializingTask> _queue = new();
        private static Task? _runningTask;
        private static string? _runningTaskName;

        private static bool _initialized;
        private static bool _allDone;

        private static ScreenText _text;
        private static ImageRenderer _image;

        private static readonly string[] _dots = ["", ".", "..", "..."];

        private static bool _finishAnimation;
        private static double _finishStartTime;
        private const float FinishDuration = 1.25f;

        public static void AddStep(InitializingTask step)
        {
            _allDone = false;
            _queue.Enqueue(step);
        }

        private static void UpdateScreen()
        {
            if (!_initialized)
            {
                _text = new ScreenText(
                    null,
                    new(Game.Instance.ClientSize.X / 2, Game.Instance.ClientSize.Y / 4 * 3),
                    new FreeTypeFont(Fonts.Default),
                    1);

                _text.Color = Vector4.One;

                _image = new ImageRenderer(
                    "Assets/Splash.png",
                    new Vector2(
                        Game.Instance.ClientSize.X / 2 - (400 * 1.75f) / 2,
                        Game.Instance.ClientSize.Y / 2 - 230),
                    new Vector2(400 * 1.75f, 100 * 1.75f));

                _initialized = true;
            }

            float offsetY = 0f;

            if (_finishAnimation)
            {
                float t = (float)((Time.ElapsedTime - _finishStartTime) / FinishDuration);

                if (t >= 1f)
                {
                    _finishAnimation = false;
                    _allDone = true;
                    return;
                }

                t = t * t * (3f - 2f * t);

                offsetY = t * Game.Instance.ClientSize.Y;
            }
            
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.ClearColor(0.1f, 0.1f, 0.1f, 1f);
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Blend);
            GL.DepthFunc(DepthFunction.Lequal);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            
            _text.Text = _runningTaskName + (_runningTask != null ? _dots[((int)(Time.ElapsedTime * 2)) % 4] : "");

            float baseTextY = Game.Instance.ClientSize.Y / 4f * 3f;
            float baseImageY = Game.Instance.ClientSize.Y / 2f - 230f;

            _text.Position = new Vector2(Game.Instance.ClientSize.X / 2f - _text.Font.GetTextWidth(_text.Text) / 2f, baseTextY + offsetY);

            _image.Position = new Vector2(
                Game.Instance.ClientSize.X / 2f - (400 * 1.75f) / 2f,
                baseImageY + offsetY);

            _image.Render(0);
            _text.Render(0);

            Game.Instance.SwapBuffers();
        }

        public static bool Render(FrameEventArgs args)
        {
            if (_allDone)
                return false;

            if (_finishAnimation)
            {
                UpdateScreen();
                return true;
            }

            if (_runningTask != null)
            {
                if (!_runningTask.IsCompleted)
                {
                    UpdateScreen();
                    return true;
                }

                if (_runningTask.IsFaulted)
                {
                    Log.Error(
                        _runningTask.Exception?.GetBaseException(),
                        "Exception during task {TaskName} occurred.",
                        _runningTaskName);
                }

                _runningTask = null;
            }

            if (!_queue.TryDequeue(out var task))
            {
                _finishAnimation = true;
                _finishStartTime = Time.ElapsedTime;
                UpdateScreen();
                return true;
            }

            _runningTaskName = task.Label ?? "Unnamed Task";
            UpdateScreen();

            Log.Debug("Executing {TaskName}", _runningTaskName);

            switch (task.Type)
            {
                case InitializingTask.TaskType.Synchronized:
                    task.Action.Invoke();
                    return true;

                case InitializingTask.TaskType.Asynchronized:
                    _runningTask = Task.Run(() => task.Action.Invoke());
                    return true;

                default:
                    Log.Error("Unknown task type: {TaskType}", task.Type);
                    Debugger.Break();
                    return true;
            }
        }
    }
}