using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core.Assets;
using RE.Editor;
using RE.Rendering.Texturing;
using RE.Utils;

namespace RE.Rendering.Renderables
{
    /// <summary>
    /// This class is used to render images in UI
    /// </summary>
    public class ImageRenderer : Renderable
    {
        private Texture _texture;
        private int _vao, _vbo, _ebo;
        private ShaderProgram _shaderProgram;
 
        public override bool IsVisible { get; set; } = true;
        public Vector2 Position { get; set; }
        public Vector2 Scale { get; set; }

        public ImageRenderer(string pathToImg, Vector2 pos, Vector2? size = null)
        {
            Position = pos;
            Scale = size ?? new Vector2(100, 100);

            _shaderProgram = CompileShaders();
            _texture = new StaticTexture(pathToImg);
            SetupQuad();
        }
        public ImageRenderer(Texture texture, Vector2 pos, Vector2? size = null)
        {
            Position = pos;
            Scale = size ?? new Vector2(100, 100);

            _shaderProgram = CompileShaders();
            _texture = texture;
            SetupQuad();
        }

        public void ReplaceImage(Texture tex, bool deleteCurrent)
        {
            if (deleteCurrent)
                _texture.Delete();
            _texture = tex;
        }
        public override void Render(FrameEventArgs args)
        {
            if (SceneEditor.Enabled)
                this.StopRender();
             
            _shaderProgram.Use();

            Matrix4 model = Matrix4.CreateScale(Scale.X, Scale.Y, 1f) * Matrix4.CreateTranslation(Position.X, Position.Y, 1);
            Matrix4 projection = Matrix4.CreateOrthographicOffCenter(0, Game.Instance.ClientSize.X, Game.Instance.ClientSize.Y, 0, -1, 1);

            _shaderProgram.SetValue("uModel", model);
            _shaderProgram.SetValue("uProjection", projection);

            GL.BindTexture(TextureTarget.Texture2D, _texture.AsOpenGl());
            GL.BindVertexArray(_vao);
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.BindVertexArray(0);
        }

        private void SetupQuad()
        {
            float[] vertices = {
                // pos       // uv
                0f,  0f,     0f, 0f,
                1f,  0f,     1f, 0f,
                1f,  1f,     1f, 1f,
                0f,  1f,     0f, 1f,
            };

            uint[] indices = { 0, 1, 2, 2, 3, 0 };

            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
            _ebo = GL.GenBuffer();

            GL.BindVertexArray(_vao);

            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            GL.BindVertexArray(0);
        }

        private ShaderProgram CompileShaders()
        {
            ShaderProgram program = new();
            program.AttachShader("assets/shaders/ui/ui_image.vert");
            program.AttachShader("assets/shaders/ui/ui_image.frag");

            return program;
        }
        public override void Dispose()
        {
            _texture.Delete();
            GL.DeleteBuffer(_vbo);
            GL.DeleteBuffer(_ebo);
            GL.DeleteVertexArray(_vao);
            _shaderProgram.Delete();
        }
    }
}
