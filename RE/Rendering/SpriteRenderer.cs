using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Utils;
using StbImageSharp;

namespace RE.Rendering
{
    public class SpriteRenderer : IRenderable, IDisposable
    {
        private int _vao, _vbo;
        private int _shaderProgram;
        private int _texture;
        private float texWidth = 0, texHeight = 0;
        private bool constantSize;
        private float scale;

        public Vector3 Position { get; set; }
        public RenderLayer RenderLayer => RenderLayer.World;
        public bool IsVisible { get; set; } = true;

        public SpriteRenderer(Vector3 position, string spritePath = "Assets/Sprites/Editor/blank.png",
            bool constantSize = false, float scale = .25f)
        {
            Position = position;
            this.constantSize = constantSize;
            this.scale = scale;

            // Вершины quad (позиция + UV)
            float[] vertices = {
            // pos      // uv
            0f, 0f,     0f, 0f,
            1f, 0f,     1f, 0f,
            1f, 1f,     1f, 1f,
            0f, 1f,     0f, 1f
        };

            uint[] indices = {
            0, 1, 2,
            2, 3, 0
        };

            // VAO/VBO
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
            int ebo = GL.GenBuffer();

            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

            GL.EnableVertexAttribArray(0); // pos
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);

            GL.EnableVertexAttribArray(1); // uv
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));

            GL.BindVertexArray(0);

            _shaderProgram = CreateShader();
            _texture = LoadTexture(spritePath);
        }

        public void Render(FrameEventArgs args)
        {
            GL.UseProgram(_shaderProgram);

            Vector3 camPos = Camera.Instance.Position;
            Vector3 lookDir = Vector3.Normalize(camPos - Position);

            Vector3 up = Vector3.UnitY; // предположим, что Y - вверх
            Vector3 right = Vector3.Normalize(Vector3.Cross(up, lookDir));
            Vector3 billboardUp = Vector3.Cross(lookDir, right);

            float aspectRatio = texWidth / texHeight;


            float w = 1f;
            float h = w / aspectRatio;
            Matrix4 translateToCenter = Matrix4.CreateTranslation(-w / 2f, -h / 2f, 0f);

            float baseSize = 1.0f; // условный "экранный" размер
            float distance = (Position - Camera.Instance.Position).Length;
            float scale = distance * baseSize;

            float size = 1.0f;

            Matrix4 model =
                translateToCenter *
                Matrix4.CreateScale(.5f) *
                Matrix4.CreateScale(w, -h, 0) *
                (constantSize ? Matrix4.CreateScale(scale * this.scale) : Matrix4.Identity) *
                Matrix4.CreateScale(size) *
                Camera.Instance.GetBillboard(Position) *
                Matrix4.CreateTranslation(Position);



            Matrix4 view = Camera.Instance.GetViewMatrix();
            Matrix4 projection = Camera.Instance.GetProjectionMatrix();

            GL.UniformMatrix4(GL.GetUniformLocation(_shaderProgram, "uModel"), false, ref model);
            GL.UniformMatrix4(GL.GetUniformLocation(_shaderProgram, "uView"), false, ref view);
            GL.UniformMatrix4(GL.GetUniformLocation(_shaderProgram, "uProjection"), false, ref projection);

            GL.BindTexture(TextureTarget.Texture2D, _texture);
            GL.BindVertexArray(_vao);
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);
        }

        public void ChangeTexture(string path) => _texture = LoadTexture(path);
        public void Dispose()
        {
            this.StopRender();
            GL.DeleteVertexArray(_vao);
            GL.DeleteBuffer(_vbo);
            GL.DeleteProgram(_shaderProgram);
            GL.DeleteTexture(_texture);
        }

        private int LoadTexture(string path)
        {
            var image = ImageResult.FromStream(File.OpenRead(path), ColorComponents.RedGreenBlueAlpha);
            (texWidth, texHeight) = (image.Width, image.Height);
            int tex = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, tex);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                image.Width, image.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, image.Data);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            return tex;
        }

        private int CreateShader()
        {
            string vertex = File.ReadAllText("Assets/shaders/sprite.vert");
            string fragment = File.ReadAllText("Assets/Shaders/sprite.frag");

            int vert = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vert, vertex);
            GL.CompileShader(vert);

            int frag = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(frag, fragment);
            GL.CompileShader(frag);

            int prog = GL.CreateProgram();
            GL.AttachShader(prog, vert);
            GL.AttachShader(prog, frag);
            GL.LinkProgram(prog);

            GL.DeleteShader(vert);
            GL.DeleteShader(frag);

            return prog;
        }
    }

}
