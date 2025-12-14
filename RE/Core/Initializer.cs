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

#pragma warning disable 8618 //these variables are initialized in Init()
        private static FreeTypeFont _font;
        private static FreeTypeFont _titleFont;
        private static ScreenText _textCurrentStep;
        private static ScreenText _textSteps;
        private static ScreenText _textTitle;
        private static ScreenText _textTitleDemo;
#pragma warning restore
        private static List<ScreenText> _textPastSteps = new();
        private static Queue<(string label, Action action)> _initSteps = new();
        private static string _currentStep = "";
        private static bool _initDone;
        private static bool _isScreenShowing;
        private static bool _shouldExecuteAction;
        private static Action? _pendingAction;
        private static int _step = 1;
        private static int _steps = 0;
        private const int MaxSteps = 10;
        public static bool HasJob { get; private set; }

        public static void Init()
        {
            _font = new(16, Fonts.Eurostile);
            _titleFont = new(64, Fonts.Eurostile);
            _textCurrentStep = new ScreenText(null, Vector2.Zero, _font);
            _textSteps = new ScreenText(null, Vector2.Zero, _font);

            var title = "REAL ENGINE";
            var titleDemo = "Demo";
            _textTitle = new ScreenText(title,
                new Vector2(
                    (Game.Instance.ClientSize.X - _titleFont.GetTextWidth(title)) / 2,
                    Game.Instance.ClientSize.Y / 4 - _titleFont.GetTextHeight(title) / 2 + 80), _titleFont, Vector4.One);
            _textTitleDemo = new ScreenText(titleDemo,
                new Vector2(
                    (Game.Instance.ClientSize.X - _titleFont.GetTextWidth(titleDemo, 0.5f)) / 2 + 200,
                    Game.Instance.ClientSize.Y / 4 - _titleFont.GetTextHeight(titleDemo, 0.5f) / 2 + 100), _titleFont, 0.5f, new(0.5f));


            _textCurrentStep.Color = new Vector4(1f, 1f, 1f, 1f);
            _textSteps.Color = new Vector4(1f, 1f, 1f, 1f);

            _textCurrentStep.StartRender();
            _textSteps.StartRender();
            _textTitle.StartRender();
            _textTitleDemo.StartRender();

            InitializationCompleted += () =>
            {
                _initDone = true;
                //_textCurrentStep.Text = "REAL ENGINE";
                //_textCurrentStep.Position = new(10, 20);
                //_textCurrentStep.Color = new Vector4(0, 0, 0, 0.345f);
                _textSteps.StopRender();
                _textCurrentStep.StopRender();
                _textTitle.StopRender();
                _textTitleDemo.StopRender();
                _textPastSteps.ForEach(s => s.StopRender());
                _isScreenShowing = false;
            };
        }

        private static void SetupScreen()
        {
            _textPastSteps.ForEach(s => s.StartRender());
            _textCurrentStep.StartRender();
            _textSteps.StartRender();
            _textTitle.StartRender();
            _textTitleDemo.StartRender();
            _isScreenShowing = true;
        }
        //todo: AddAsyncStep
        public static void AddStep((string label, Action action) step)
        {
            _initDone = false;
            _initSteps.Enqueue(step);
            _steps++;
        }

        public static bool Render(FrameEventArgs args)
        {
            if (!_initDone)
            {
                if(!_isScreenShowing)
                    SetupScreen();

                if (_shouldExecuteAction)
                {
                    Serilog.Log.Information(_currentStep);
                    _pendingAction?.Invoke();
                    _shouldExecuteAction = false;
                    _pendingAction = null;

                    if (!string.IsNullOrEmpty(_currentStep))
                    {
                        HasJob = true;
                        var pastText = new ScreenText(_currentStep, Vector2.Zero, _font, new Vector4(Vector3.One, .175f));
                        pastText.StartRender();
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

                    float textX = (Game.Instance.ClientSize.X - _font.GetTextWidth(label)) / 2f;
                    float textY = (Game.Instance.ClientSize.Y - _font.GetTextHeight(label)) / 2f + 50;

                    _textCurrentStep.Position = new(textX, textY);
                    _textCurrentStep.Text = label;

                    float centerX = Game.Instance.ClientSize.X / 2f;
                    float centerY = Game.Instance.ClientSize.Y / 2f + 50;

                    _textCurrentStep.Text = _currentStep;
                    _textCurrentStep.Position = new Vector2(centerX - _font.GetTextWidth(_currentStep) / 2f, centerY);

                    for (int i = 0; i < _textPastSteps.Count; i++)
                    {
                        var txt = _textPastSteps[i];
                        float y = centerY + _font.PixelHeight * (i + 1);
                        txt.Position = new Vector2(centerX - _font.GetTextWidth(txt.Text) / 2f, y);

                        float t = i / (float)(MaxSteps - 1); //[0; 1]
                        float alpha = MathF.Pow(1f - t, 1.5f); // [0,18; 1]]

                        float x = alpha;
                        float fromMin = 0f, fromMax = 1;
                        float toMin = 0.01f, toMax = 0.25f;

                        float result = toMin + (x - fromMin) / (fromMax - fromMin) * (toMax - toMin);

                        txt.Color = txt.Color with { W = result };
                    }

                    _textSteps.Text = $"{_step++}/{_steps}";
                    _textSteps.Position =
                        new Vector2((Game.Instance.ClientSize.X - _font.GetTextWidth(_textSteps.Text)) / 2,
                            (Game.Instance.ClientSize.Y - _font.GetTextHeight(_textSteps.Text)) / 2 - 20 + 50);

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

            HasJob = false;
            return false;
        }
    }
}