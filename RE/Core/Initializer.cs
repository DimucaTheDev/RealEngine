using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Rendering;
using RE.Rendering.Renderables;
using RE.Rendering.Text;
using RE.Utils;

namespace RE.Core
{
    public sealed class Initializer
    {
        public static event Action? InitializationCompleted;

        private static FreeTypeFont font;
        private static FreeTypeFont titleFont;
        private static ScreenText _textCurrentStep;
        private static ScreenText _textSteps;
        private static List<ScreenText> _textPastSteps = new();
        private static ScreenText _textTitle;
        private static Queue<(string label, Action action)> _initSteps = new();
        private static string _currentStep = "";
        private static bool _initDone = false;
        private static bool _shouldExecuteAction;
        private static Action? _pendingAction;
        private static int _step = 1;
        private static int _steps = 0;
        private const int MaxSteps = 10;
        private static string pastLog = "";

        public static void Init()
        {
            font = new(16, Fonts.Eurostile);
            titleFont = new(64, Fonts.Eurostile);


            _textCurrentStep = new ScreenText(null, Vector2.Zero, font);
            _textSteps = new ScreenText(null, Vector2.Zero, font);

            var title = "REAL ENGINE";
            _textTitle = new ScreenText(title,
                new Vector2(
                    (Game.Instance.ClientSize.X - titleFont.GetTextWidth(title)) / 2,
                    Game.Instance.ClientSize.Y / 4 - titleFont.GetTextHeight(title) / 2 + 80), titleFont, Vector4.One);


            _textCurrentStep.Color = new Vector4(1f, 1f, 1f, 1f);
            _textSteps.Color = new Vector4(1f, 1f, 1f, 1f);

            _textCurrentStep.Render();
            _textSteps.Render();
            _textTitle.Render();

            InitializationCompleted += () =>
            {
                _initDone = true;
                //_textCurrentStep.Content = "REAL ENGINE";
                //_textCurrentStep.Position = new(10, 20);
                //_textCurrentStep.Color = new Vector4(0, 0, 0, 0.345f);
                _textSteps.StopRender();
                _textCurrentStep.StopRender();
                _textTitle.StopRender();
                _textPastSteps.ForEach(s => s.StopRender());
            };
        }

        private static void SetupScreen()
        {
            _textPastSteps.ForEach(s => s.Render());
            _textCurrentStep.Render();
            _textSteps.Render();
            _textTitle.Render();
        }
        public static void AddStep((string label, Action action) step)
        {
            if (!_initSteps.Any()) SetupScreen();

            _initDone = false;
            _initSteps.Enqueue(step);
            _steps++;
        }

        public static bool Render(FrameEventArgs args)
        {
            if (!_initDone)
            {
                if (_shouldExecuteAction)
                {
                    Serilog.Log.Information(_currentStep);
                    _pendingAction?.Invoke();
                    _shouldExecuteAction = false;
                    _pendingAction = null;

                    if (!string.IsNullOrEmpty(_currentStep))
                    {
                        var pastText = new ScreenText(_currentStep, Vector2.Zero, font, new Vector4(Vector3.One, .175f));
                        pastText.Render();
                        _textPastSteps.Insert(0, pastText);

                        if (_textPastSteps.Count > MaxSteps)
                        {
                            _textPastSteps.Last().StopRender();
                            _textPastSteps.RemoveAt(_textPastSteps.Count - 1);
                        }
                    }
                }

                if (_initSteps.Count > 0)
                {
                    var (label, action) = _initSteps.Dequeue();
                    _currentStep = label;

                    float textX = (Game.Instance.ClientSize.X - font.GetTextWidth(label)) / 2f;
                    float textY = (Game.Instance.ClientSize.Y - font.GetTextHeight(label)) / 2f + 50;

                    _textCurrentStep.Position = new(textX, textY);
                    _textCurrentStep.Content = label;

                    float centerX = Game.Instance.ClientSize.X / 2f;
                    float centerY = Game.Instance.ClientSize.Y / 2f + 50;

                    _textCurrentStep.Content = _currentStep;
                    _textCurrentStep.Position = new Vector2(centerX - font.GetTextWidth(_currentStep) / 2f, centerY);

                    for (int i = 0; i < _textPastSteps.Count; i++)
                    {
                        var txt = _textPastSteps[i];
                        float y = centerY + font.PixelHeight * (i + 1);
                        txt.Position = new Vector2(centerX - font.GetTextWidth(txt.Content) / 2f, y);

                        float t = i / (float)(MaxSteps - 1); //[0; 1]
                        float alpha = MathF.Pow(1f - t, 1.5f); // [0,18; 1]]

                        float x = alpha;
                        float fromMin = 0f, fromMax = 1;
                        float toMin = 0.01f, toMax = 0.25f;

                        float result = toMin + (x - fromMin) / (fromMax - fromMin) * (toMax - toMin);

                        txt.Color = txt.Color with { W = result };
                    }

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

                RenderManager.RenderType(_textCurrentStep, args);
                RenderManager.RenderType(_textSteps, args);

                Game.Instance.SwapBuffers();
                return true;
            }

            return false;
        }
    }
}
