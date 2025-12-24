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
        private Texture _texture;
        private float _texWidth = 0, _texHeight = 0;
        private readonly bool _constantSize;
        private readonly ShaderProgram _shaderProgram;

        private static readonly Dictionary<string, (Texture texture, int width, int height)> TextureCache = new();
         
        public Vector3 Position { get; set; }
        public override RenderLayer RenderLayer => RenderLayer.World;
        public override bool IsVisible { get; set; } = true;
        public string Path { get; set; }
        public float Scale { get; set; }
        public bool LockRotationX { get; set; } = false;
        public bool LockRotationY { get; set; } = false;
        public bool LockRotationZ { get; set; } = false;

        public SpriteRenderer(Vector3 position, string spritePath = "Assets/Sprites/Editor/blank.png",
            bool constantSize = false, float scale = 1f)
        {
            Position = position;
            Path = spritePath;
            Scale = scale;
            _constantSize = constantSize;

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

            float aspectRatio = _texWidth / _texHeight;
            float baseSize = 1.0f;
            float distance = (Position - Camera.GetActiveCamera().Position).Length;
            float scale = distance * baseSize;

            float size = 1.0f;

            Matrix4 translateToCenter = Matrix4.CreateTranslation(-0.5f, -0.5f, 0f);
            float finalScale = _constantSize ? scale * Scale : size * Scale;
            Matrix4 billboard = Camera.GetActiveCamera().GetBillboard(Position);

            // lock axes
            if (LockRotationX || LockRotationY || LockRotationZ)
            {
                // extract rotation basis
                var right = billboard.Row0.Xyz;
                var up = billboard.Row1.Xyz;
                var forward = billboard.Row2.Xyz;

                if (LockRotationX)
                    right = Vector3.UnitX;
                if (LockRotationY)
                    up = Vector3.UnitY;
                if (LockRotationZ)
                    forward = Vector3.UnitZ;

                billboard.Row0.Xyz = right;
                billboard.Row1.Xyz = up;
                billboard.Row2.Xyz = forward;
            }

            Matrix4 model =
                translateToCenter *
                Matrix4.CreateScale(finalScale, -finalScale / aspectRatio, 1f) *
                billboard *
                Matrix4.CreateTranslation(Position);


            var view = Camera.GetActiveCamera().GetViewMatrix();
            var projection = Camera.GetActiveCamera().GetProjectionMatrix();

            _shaderProgram.SetValue("uModel", model);
            _shaderProgram.SetValue("uView", view);
            _shaderProgram.SetValue("uProjection", projection);

            GL.BindTexture(TextureTarget.Texture2D, _texture.AsOpenGl());
            GL.BindVertexArray(_vao);
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);
        }

        public void SetTexture(Texture texture) => _texture = texture;
        public void ChangeTexture(string path)
        {
            if (Path == path)
                return;
            _texture = LoadTexture(path);
        }

        public override void Dispose()
        {
            this.StopRender();
            GL.DeleteVertexArray(_vao);
            GL.DeleteBuffer(_vbo);
            _shaderProgram.Delete();

            //this comment prevents lagyshit-9000 from happening in particle renderer
            //GL.DeleteTexture(_texture); 
        }

        private Texture LoadTexture(string path)
        {
            Path = path;
            if (TextureCache.TryGetValue(path, out var t))
            {
                _texWidth = t.width;
                _texHeight = t.height;
                return t.texture;
            }

            if (!ContentManager.Exists(path))
            {
                Log.Error("Texture at path {Path} does not exist!", path);
                var missingTexture = Texture.CreateMissingTexture(4);

                return missingTexture;
            }

            var texture = new Texture(path);

            (_texWidth, _texHeight) = (texture.Width, texture.Height);
            
            TextureCache.Add(path, (texture, texture.Width, texture.Height));
            return texture;
        }
    }
}
