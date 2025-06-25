using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;

namespace RE.Rendering.Skybox
{
    internal class SkyboxRenderer : IRenderable
    {

        public static SkyboxRenderer Instance { get; private set; }

        public RenderLayer RenderLayer => RenderLayer.Skybox;
        public bool IsVisible { get; set; } = true;

        private static int _vao, _vbo, _handle;

        private static readonly float[] _cubeVertices = {
            // Только позиции, без цветов, без нормалей
            -1,  1, -1,  -1, -1, -1,   1, -1, -1,
            1, -1, -1,   1,  1, -1,  -1,  1, -1, // задняя
            -1, -1,  1,  -1, -1, -1,  -1,  1, -1,
            -1,  1, -1,  -1,  1,  1,  -1, -1,  1, // левая
            1, -1, -1,   1, -1,  1,   1,  1,  1,
            1,  1,  1,   1,  1, -1,   1, -1, -1, // правая
            -1, -1,  1,  -1,  1,  1,   1,  1,  1,
            1,  1,  1,   1, -1,  1,  -1, -1,  1, // передняя
            -1,  1, -1,   1,  1, -1,   1,  1,  1,
            1,  1,  1,  -1,  1,  1,  -1,  1, -1, // верх
            -1, -1, -1,  -1, -1,  1,   1, -1,  1,
            1, -1,  1,   1, -1, -1,  -1, -1, -1  // низ
        };

        public static void Init()
        {

            Instance = new SkyboxRenderer(); // added instance initialization

            var vertexSource = File.ReadAllText("Assets/shaders/skybox.vert");
            var fragmentSource = File.ReadAllText("Assets/shaders/skybox.frag");

            var vertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertexShader, vertexSource);
            GL.CompileShader(vertexShader);

            var fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragmentShader, fragmentSource);
            GL.CompileShader(fragmentShader);

            _handle = GL.CreateProgram();
            GL.AttachShader(_handle, vertexShader);
            GL.AttachShader(_handle, fragmentShader);
            GL.LinkProgram(_handle);

            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);


            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();

            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, _cubeVertices.Length * sizeof(float), _cubeVertices, BufferUsageHint.StaticDraw);

            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            RenderLayerManager.AddRenderable(Instance, typeof(SkyboxRenderer));
        }

        public void Render(FrameEventArgs args)
        {
            GL.DepthMask(false); // не пишем в z-buffer

            GL.UseProgram(_handle);
            var view = Camera.Camera.Instance.GetViewMatrix();
            var proj = Camera.Camera.Instance.GetProjectionMatrix();
            view.Row3.X = 0;
            view.Row3.Y = 0;
            view.Row3.Z = 0;
            GL.UniformMatrix4(GL.GetUniformLocation(_handle, "view"), false, ref view);
            GL.UniformMatrix4(GL.GetUniformLocation(_handle, "projection"), false, ref proj); // added ref

            GL.BindVertexArray(_vao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 36);

            GL.DepthMask(true);
        }
    }
}
