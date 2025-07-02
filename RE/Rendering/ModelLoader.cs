using Assimp;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Rendering.Text;
using RE.Utils;
using Serilog;
using StbImageSharp;
using PrimitiveType = OpenTK.Graphics.OpenGL4.PrimitiveType;
using Quaternion = OpenTK.Mathematics.Quaternion;

namespace RE.Rendering
{
    public class ModelRenderer : Renderable
    {
        private static readonly FreeTypeFont _font = new(32, "Assets/Fonts/consola.ttf");

        private int _vao, _vbo, _ebo, _texture, _shader;
        private int _indexCount;
        private BillboardText3D? _text;
        private SpriteRenderer? _noModelSprite;
        private bool modelLoaded = false;

        public override RenderLayer RenderLayer => RenderLayer.World;
        public override bool IsVisible { get; set; } = true;
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
        public Vector3 Scale { get; set; }

        public ModelRenderer(string path, Vector3? pos = null, Quaternion? rot = null, Vector3? scale = null)
        {
            Position = pos ?? Vector3.Zero;
            Rotation = rot ?? Quaternion.Identity;
            Scale = scale ?? Vector3.One;

            if (!(modelLoaded = LoadModel(path)))
            {
                _noModelSprite = new SpriteRenderer(Position, "Assets/Sprites/Editor/no_model.png");
                _text = new BillboardText3D(path, Position + new Vector3(0, .5f, 0), _font, true);

                _noModelSprite.Render();
                _text.Render();

                return;
            }
            InitShader();
        }

        public override void AddedToRenderList()
        {
            if (!modelLoaded)
            {
                _noModelSprite?.Render();
                _text?.Render();
            }
        }

        public override void RemovedFromRenderList()
        {
            if (_noModelSprite?.IsRendering() ?? false) _noModelSprite?.StopRender();
            if (_text?.IsRendering() ?? false) _text?.StopRender();
        }

        private bool LoadModel(string path)
        {
            using AssimpContext importer = new AssimpContext();
            Scene scene;
            try
            {
                scene = importer.ImportFile(path,
                   PostProcessSteps.Triangulate | PostProcessSteps.GenerateNormals | PostProcessSteps.FlipUVs);
            }
            catch (FileNotFoundException ex)
            {
                Log.Error(ex, $"Model file not found: {Path.GetRelativePath(".", path)}");
                return false;
            }
            catch (AssimpException ex)
            {
                Log.Error(ex, $"Failed to load model from {Path.GetRelativePath(".", path)}");
                return false;
            }
            if (!scene.Meshes.Any())
            {
                Log.Error($"No meshes found in model file({Path.GetRelativePath(".", path)})");
                return false;
            }


            var mesh = scene.Meshes[0];
            var vertices = new List<float>();
            var indices = new List<uint>();

            for (int i = 0; i < mesh.VertexCount; i++)
            {
                var pos = mesh.Vertices[i];
                var uv = mesh.HasTextureCoords(0) ? mesh.TextureCoordinateChannels[0][i] : new Vector3D(0, 0, 0);
                vertices.AddRange([pos.X, pos.Y, pos.Z, uv.X, uv.Y]);
            }

            foreach (var face in mesh.Faces)
                indices.AddRange(face.Indices.Select(s => (uint)s));

            _indexCount = indices.Count;

            // OpenGL
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
            _ebo = GL.GenBuffer();

            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Count * sizeof(float), vertices.ToArray(), BufferUsageHint.StaticDraw);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Count * sizeof(uint), indices.ToArray(), BufferUsageHint.StaticDraw);

            // layout: pos (3), uv (2)
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float), 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            GL.BindVertexArray(0);

            if (!scene.Materials.Any())
            {
                Log.Warning($"No materials found in the model ({Path.GetRelativePath(".", path)})");
            }
            else
            {
                var mat = scene.Materials[mesh.MaterialIndex];

                var texPath = mat.HasTextureDiffuse ? mat.TextureDiffuse.FilePath : null;


                if (texPath != null && File.Exists(texPath))
                {
                    _texture = LoadTexture(texPath);
                    return true;
                }
                if (mat.HasTextureDiffuse)
                {
                    _texture = GL.GenTexture();
                    GL.BindTexture(TextureTarget.Texture2D, _texture);

                    var img = ImageResult.FromMemory(scene.Textures.First().CompressedData, ColorComponents.RedGreenBlueAlpha);

                    GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                        img.Width, img.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte,
                        img.Data);

                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                        (int)TextureMinFilter.Nearest);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                        (int)TextureMagFilter.Nearest);

                    GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);


                    vertices.Clear();
                    indices.Clear();


                    return true;
                }
            }


            Log.Warning($"No texture found for model {Path.GetRelativePath(".", path)}");
            _texture = CreateMissingTexture();
            return true;
        }

        private int LoadTexture(string filePath)
        {
            int texID = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, texID);

            using var stream = File.OpenRead(filePath);
            var img = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                img.Width, img.Height, 0, OpenTK.Graphics.OpenGL4.PixelFormat.Rgba, PixelType.UnsignedByte, img.Data);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            return texID;
        }

        private int CreateMissingTexture()
        {
            const int size = 100;
            int texID = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, texID);

            byte[] data = new byte[size * size * 4];

            byte[] purple = { 255, 0, 255, 255 };
            byte[] black = { 0, 0, 0, 255 };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isPurple = ((x + y) % 2 == 0);
                    byte[] color = isPurple ? purple : black;

                    int index = (y * size + x) * 4;
                    System.Buffer.BlockCopy(color, 0, data, index, 4);
                }
            }

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                size, size, 0, PixelFormat.Rgba, PixelType.UnsignedByte, data);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

            return texID;
        }

        private void InitShader()
        {
            string vertexShaderSrc = File.ReadAllText("assets/shaders/assimp.vert");

            string fragmentShaderSrc = File.ReadAllText("assets/shaders/assimp.frag");

            int vs = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vs, vertexShaderSrc);
            GL.CompileShader(vs);
            GL.GetShader(vs, ShaderParameter.CompileStatus, out var status);
            if (status == 0) throw new Exception(GL.GetShaderInfoLog(vs));

            int fs = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fs, fragmentShaderSrc);
            GL.CompileShader(fs);
            GL.GetShader(fs, ShaderParameter.CompileStatus, out status);
            if (status == 0) throw new Exception(GL.GetShaderInfoLog(fs));

            _shader = GL.CreateProgram();
            GL.AttachShader(_shader, vs);
            GL.AttachShader(_shader, fs);
            GL.LinkProgram(_shader);

            GL.DeleteShader(vs);
            GL.DeleteShader(fs);
        }

        public override void Render(FrameEventArgs args)
        {
            Matrix4 model =
                Matrix4.CreateRotationX(MathHelper.DegreesToRadians(-90)) *
                Matrix4.CreateFromQuaternion(Rotation) *
                Matrix4.CreateTranslation(Position);

            Matrix4 view = Camera.Instance.GetViewMatrix();
            Matrix4 proj = Camera.Instance.GetProjectionMatrix();

            GL.UseProgram(_shader);
            GL.UniformMatrix4(GL.GetUniformLocation(_shader, "model"), false, ref model);
            GL.UniformMatrix4(GL.GetUniformLocation(_shader, "view"), false, ref view);
            GL.UniformMatrix4(GL.GetUniformLocation(_shader, "projection"), false, ref proj);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _texture);
            GL.Uniform1(GL.GetUniformLocation(_shader, "tex"), 0);

            GL.BindVertexArray(_vao);
            GL.DrawElements(PrimitiveType.Triangles, _indexCount, DrawElementsType.UnsignedInt, 0);
            GL.BindVertexArray(0);
        }

        public override void Dispose()
        {
            this.StopRender();
            _text?.StopRender();
            _noModelSprite?.StopRender();
            _text?.Dispose();
            _noModelSprite?.Dispose();
            GL.DeleteVertexArray(_vao);
            GL.DeleteBuffer(_vbo);
            GL.DeleteBuffer(_ebo);
            GL.DeleteTexture(_texture);
            GL.DeleteProgram(_shader);
        }
    }

}
