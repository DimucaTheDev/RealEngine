using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Rendering;
using RE.Rendering.Text;
using RE.Utils;

namespace RE.Core
{
    public sealed class Initializer
    {
        public static event Action? InitializationCompleted;

        private static FreeTypeFont font;
        private static FreeTypeFont titleFont;
        private static Text _textCurrentStep;
        private static Text _textSteps;
        private static Text _textPastSteps;
        private static Text _textTitle;
        private static Queue<(string label, Action action)> _initSteps = new();
        private static string _currentStep = "";
        private static bool _initDone = false;
        private static bool _shouldExecuteAction;
        private static Action? _pendingAction;
        private static int _step = 1;
        private static int _steps = 0;

        private static string pastLog = "";

        public static void Init()
        {
            font = new(16, Fonts.Eurostile);
            titleFont = new(64, Fonts.Eurostile);


            _textCurrentStep = new Text(null, Vector2.Zero, font);
            _textSteps = new Text(null, Vector2.Zero, font);
            _textPastSteps = new Text(null, Vector2.Zero, font, new Vector4(Vector3.One, .25f));

            var title = "REAL ENGINE";
            _textTitle = new Text(title,
                new Vector2(
                    (Game.Instance.ClientSize.X - titleFont.GetTextWidth(title)) / 2,
                    Game.Instance.ClientSize.Y / 4 - titleFont.GetTextHeight(title) / 2 + 80), titleFont, Vector4.One);


            _textCurrentStep.Color = new Vector4(1f, 1f, 1f, 1f);
            _textSteps.Color = new Vector4(1f, 1f, 1f, 1f);

            _textCurrentStep.Render();
            _textSteps.Render();
            _textPastSteps.Render();
            _textTitle.Render();

            InitializationCompleted += () =>
            {
                _initDone = true;
                _textCurrentStep.Content = "REAL ENGINE";
                _textCurrentStep.Position = new(10, 20);
                _textCurrentStep.Color = new Vector4(0, 0, 0, 0.345f);
                _textSteps.StopRender();
                _textTitle.StopRender();
                _textPastSteps.StopRender();
            };
        }

        public static void AddStep((string label, Action action) step)
        {
            _initSteps.Enqueue(step);
            _steps = _initSteps.Count;
        }

        public static bool Render(FrameEventArgs args)
        {
            if (!_initDone)
            {
                // Выполнить отложенное действие после отображения текста
                if (_shouldExecuteAction)
                {
                    Serilog.Log.Information(_currentStep);
                    _pendingAction?.Invoke();
                    _shouldExecuteAction = false;
                    _pendingAction = null;
                    pastLog = pastLog.Insert(0, $"{_currentStep}\n");
                }

                // Загрузить следующий шаг
                if (_initSteps.Count > 0)
                {
                    var (label, action) = _initSteps.Dequeue();
                    _currentStep = label;

                    float textX = (Game.Instance.ClientSize.X - font.GetTextWidth(label)) / 2f;
                    float textY = (Game.Instance.ClientSize.Y - font.GetTextHeight(label)) / 2f + 50;

                    _textCurrentStep.Position = new(textX, textY);
                    _textCurrentStep.Content = label;

                    _textPastSteps.Content = pastLog;
                    _textPastSteps.Position = new Vector2(textX, textY + font.PixelHeight);

                    _textSteps.Content = $"{_step++}/{_steps}";
                    _textSteps.Position =
                        new Vector2((Game.Instance.ClientSize.X - font.GetTextWidth(_textSteps.Content)) / 2,
                            (Game.Instance.ClientSize.Y - font.GetTextHeight(_textSteps.Content)) / 2 - 20 + 50);


                    _shouldExecuteAction = true;
                    _pendingAction = action;
                }
                else
                {
                    InitializationCompleted?.Invoke();
                }

                GL.ClearColor(0.1f, 0.1f, 0.1f, 1f);
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

                RenderLayerManager.RenderType(_textCurrentStep, args);
                RenderLayerManager.RenderType(_textSteps, args);

                Game.Instance.SwapBuffers();
                return true;
            }

            return false;
        }
    }
}
