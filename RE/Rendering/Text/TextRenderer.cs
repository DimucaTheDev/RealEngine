using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core;

namespace RE.Rendering.Text
{
    internal class TextRenderer
    {
        private static int shaderProgram;
        public static void Init()
        {
            int vertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertexShader, File.ReadAllText("assets/shaders/text.vert"));
            GL.CompileShader(vertexShader);

            // Проверка на ошибки компиляции (по желанию)
            GL.GetShader(vertexShader, ShaderParameter.CompileStatus, out int vStatus);
            if (vStatus != (int)All.True)
            {
                string infoLog = GL.GetShaderInfoLog(vertexShader);
                throw new Exception($"Ошибка компиляции вершинного шейдера: {infoLog}");
            }

            // Компиляция фрагментного шейдера
            int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragmentShader, File.ReadAllText("assets/shaders/text.frag"));
            GL.CompileShader(fragmentShader);

            // Проверка на ошибки компиляции
            GL.GetShader(fragmentShader, ShaderParameter.CompileStatus, out int fStatus);
            if (fStatus != (int)All.True)
            {
                string infoLog = GL.GetShaderInfoLog(fragmentShader);
                throw new Exception($"Ошибка компиляции фрагментного шейдера: {infoLog}");
            }

            // Создание и линковка программы
            shaderProgram = GL.CreateProgram();
            GL.AttachShader(shaderProgram, vertexShader);
            GL.AttachShader(shaderProgram, fragmentShader);
            GL.LinkProgram(shaderProgram);

            // Проверка на ошибки линковки
            GL.GetProgram(shaderProgram, GetProgramParameterName.LinkStatus, out int linkStatus);
            if (linkStatus != (int)All.True)
            {
                string infoLog = GL.GetProgramInfoLog(shaderProgram);
                throw new Exception($"Ошибка линковки программы: {infoLog}");
            }

            // Удаление шейдеров после линковки
            GL.DetachShader(shaderProgram, vertexShader);
            GL.DetachShader(shaderProgram, fragmentShader);
            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);


            RenderLayerManager.AddRenderableInitAction(typeof(Text), () =>
            {
                Matrix4 projectionM = Matrix4.CreateOrthographicOffCenter(0.0f, Game.Instance.ClientSize.X, Game.Instance.ClientSize.Y, 0.0f, -1.0f, 1.0f);
                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(0, BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
                GL.UseProgram(shaderProgram);
                GL.UniformMatrix4(1, false, ref projectionM);
            });

        }

        private static FreeTypeFont _font = new FreeTypeFont(32, @"C:\Users\DimucaTheDev\AppData\Local\Microsoft\Windows\Fonts\minecraft-unicode-version-0-1-0.ttf");

        public static void Render(FrameEventArgs args)
        {
            Matrix4 projectionM = Matrix4.CreateOrthographicOffCenter(0.0f, Game.Instance.ClientSize.X, Game.Instance.ClientSize.Y, 0.0f, -1.0f, 1.0f);

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(0, BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
            GL.UseProgram(shaderProgram);
            GL.UniformMatrix4(1, false, ref projectionM);

            GL.Uniform4(3, new Vector4(0, 0, 0, .2f));


            _font.RenderText(log, 5.0f, 15.0f, .45f, new Vector2(1.0f, 0));

            GL.Uniform4(3, new Vector4(0, 0, 0, 0));

            GL.Uniform3(2, new Vector3(0.3f, 0.1f, 0.9f));
            _font.RenderText(Math.Round(1 / args.Time).ToString(), 50.0f, 200.0f, 0.9f, new Vector2(1.0f, -0.25f));

            if (Random.Shared.Next(100) > 90)
            {
                Log("Random message: " + Random.Shared.Next(1000));
            }
        }

        private static string log = "";
        static void Log(string message)
        {
            log = message + "\n" + log;
            Console.WriteLine(message);
        }
    }
}
