using Microsoft.VisualBasic.Logging;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core.Assets;
using RE.Utils;
using StbImageSharp;
using Log = Serilog.Log;

namespace RE.Rendering.Renderables
{
    public class SpriteRenderer : Renderable
    {
        private readonly int _vao;
        private readonly int _vbo; 
        private int _texture;
        private float _texWidth = 0, _texHeight = 0;
        private readonly bool _constantSize;
        private readonly ShaderProgram _shaderProgram;

        private static readonly Dictionary<string, (int texture, int width, int height)> TextureCache = new();


        public Vector3 Position { get; set; }
        public override RenderLayer RenderLayer => RenderLayer.World;
        public override bool IsVisible { get; set; } = true;
        public string Path { get; set; }
        public float Scale { get; set; }

        public SpriteRenderer(Vector3 position, string spritePath = "Assets/Sprites/Editor/blank.png",
            bool constantSize = false, float scale = 1f)
        {
            Position = position;
            this._constantSize = constantSize;
            Scale = scale;

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

            _shaderProgram = new ShaderProgram();
            _shaderProgram.AttachShader("Assets/shaders/sprite.vert");
            _shaderProgram.AttachShader("Assets/Shaders/sprite.frag");

            _texture = LoadTexture(spritePath);
        }

        public override void Render(FrameEventArgs args)
        {
            _shaderProgram.Use();

            var camPos = Camera.Instance.Position;
            var lookDir = Vector3.Normalize(camPos - Position);

            var up = Vector3.UnitY;
            var right = Vector3.Normalize(Vector3.Cross(up, lookDir));
            var billboardUp = Vector3.Cross(lookDir, right);

            var aspectRatio = _texWidth / _texHeight;


            var w = 1f;
            var h = w / aspectRatio;

            var baseSize = 1.0f;
            var distance = (Position - Camera.Instance.Position).Length;
            var scale = distance * baseSize;

            var size = 1.0f;

            var translateToCenter = Matrix4.CreateTranslation(-0.5f, -0.5f, 0f);
            var finalScale = _constantSize ? scale * Scale : size * Scale;
            var model =
                translateToCenter *
                Matrix4.CreateScale(finalScale, -finalScale / aspectRatio, 1f) *
                Camera.Instance.GetBillboard(Position) *
                Matrix4.CreateTranslation(Position);

            var view = Camera.Instance.GetViewMatrix();
            var projection = Camera.Instance.GetProjectionMatrix();

            GL.UniformMatrix4(GL.GetUniformLocation(_shaderProgram, "uModel"), false, ref model);
            GL.UniformMatrix4(GL.GetUniformLocation(_shaderProgram, "uView"), false, ref view);
            GL.UniformMatrix4(GL.GetUniformLocation(_shaderProgram, "uProjection"), false, ref projection);

            GL.BindTexture(TextureTarget.Texture2D, _texture);
            GL.BindVertexArray(_vao);
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);
        }

        public void SetTexture(uint id) => _texture = (int)id;
        public void ChangeTexture(string path)
        {
            if (Path == path)
                return;
            _texture = LoadTexture(path);
        }

        public void Dispose()
        {
            this.StopRender();
            GL.DeleteVertexArray(_vao);
            GL.DeleteBuffer(_vbo);
            GL.DeleteProgram(_shaderProgram);

            //this comment prevents lagyshit-9000 from happening in particle renderer
            //GL.DeleteTexture(_texture); 
        }

        private int LoadTexture(string path)
        {
            Path = path;
            if (TextureCache.TryGetValue(path, out var t))
            {
                _texWidth = t.width;
                _texHeight = t.height;
                return t.texture;
            }

            if (!File.Exists(path))
            {
                Log.Error("Texture at path {Path} does not exist!", path);
                var missingTexture = (int)Util.CreateMissingTexture(4);

                return missingTexture;
            }
            var image = ImageResult.FromStream(File.OpenRead(path), ColorComponents.RedGreenBlueAlpha);
            (_texWidth, _texHeight) = (image.Width, image.Height);
            int tex = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, tex);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                image.Width, image.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, image.Data);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

            TextureCache.Add(path, (tex, image.Width, image.Height));
            return tex;
        } 
    } 
}
