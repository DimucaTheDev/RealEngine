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

namespace RE.Rendering.Renderables
{
    public class ModelRenderer : Renderable
    {
        private static readonly FreeTypeFont _font = new(32, "Assets/Fonts/consola.ttf");
        private static readonly Dictionary<string, (int vao, int vbo, int ebo, int indexCount)> _meshCache = new();
        private static readonly Dictionary<string, int> _textureCache = new();
        private static int _sharedShader;
        private static bool _shaderInitialized = false;

        public Scene AssimpScene;

        private int _vao, _vbo, _ebo, _texture;
        private int _indexCount;
        private FloatingText? _text;
        private SpriteRenderer? _noModelSprite;
        private bool modelLoaded = false;

        public override RenderLayer RenderLayer => RenderLayer.World;
        public override bool IsVisible { get; set; } = true;
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
        public Vector3 Scale { get; set; }
        public Matrix4 RotationMatrix { get; set; }


        public ModelRenderer(string path, Vector3? pos = null, Quaternion? rot = null, Vector3? scale = null)
        {
            Position = pos ?? Vector3.Zero;
            Rotation = rot ?? Quaternion.Identity;
            Scale = scale ?? Vector3.One;

            if (!(modelLoaded = LoadModel(path)))
            {
                _noModelSprite = new SpriteRenderer(Position, "Assets/Sprites/Editor/no_model.png");
                _text = new FloatingText(path, Position + new Vector3(0, .5f, 0), _font, true);

                _noModelSprite.Render();
                _text.Render();

                return;
            }
            InitShader();
        }
        public override void Render(FrameEventArgs args)
        {
            if (!RenderManager.IsSphereInFrustum(new(Position.X, Position.Y, Position.Z), 1))
                return;

            Matrix4 model =
                Matrix4.CreateScale(Scale) *
                Matrix4.CreateFromQuaternion(Rotation) *
                Matrix4.CreateTranslation(Position);

            Matrix4 view = Camera.Instance.GetViewMatrix();
            Matrix4 proj = Camera.Instance.GetProjectionMatrix();

            GL.UseProgram(_sharedShader);
            GL.UniformMatrix4(GL.GetUniformLocation(_sharedShader, "model"), false, ref model);
            GL.UniformMatrix4(GL.GetUniformLocation(_sharedShader, "view"), false, ref view);
            GL.UniformMatrix4(GL.GetUniformLocation(_sharedShader, "projection"), false, ref proj);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _texture);
            GL.Uniform1(GL.GetUniformLocation(_sharedShader, "tex"), 0);

            GL.BindVertexArray(_vao);
            GL.DrawElements(PrimitiveType.Triangles, _indexCount, DrawElementsType.UnsignedInt, 0);
            GL.BindVertexArray(0);
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
            if (_meshCache.TryGetValue(path, out var meshData))
            {
                (_vao, _vbo, _ebo, _indexCount) = meshData;
                _texture = CreateMissingTexture(); //GetOrLoadTexture(path);
                return true;
            }

            using AssimpContext importer = new AssimpContext();
            Scene scene;
            try
            {
                AssimpScene = scene = importer.ImportFile(path,
                     PostProcessSteps.Triangulate | PostProcessSteps.GenerateNormals | PostProcessSteps.FlipUVs);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to load model: {path}");
                return false;
            }

            if (!scene.Meshes.Any()) return false;
            var mesh = scene.Meshes[0];

            var vertices = new List<float>();
            var indices = new List<uint>();

            var min = new Vector3D(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3D(float.MinValue, float.MinValue, float.MinValue);

            for (int i = 0; i < mesh.VertexCount; i++)
            {
                var v = mesh.Vertices[i];
                min.X = Math.Min(min.X, v.X);
                min.Y = Math.Min(min.Y, v.Y);
                min.Z = Math.Min(min.Z, v.Z);

                max.X = Math.Max(max.X, v.X);
                max.Y = Math.Max(max.Y, v.Y);
                max.Z = Math.Max(max.Z, v.Z);
            }

            var center = (min + max) * 0.5f;

            OpenTK.Mathematics.Quaternion correctionRotation = OpenTK.Mathematics.Quaternion.FromAxisAngle(OpenTK.Mathematics.Vector3.UnitX, MathHelper.DegreesToRadians(-90.0f));

            for (int i = 0; i < mesh.VertexCount; i++)
            {
                var pos = mesh.Vertices[i] - center;
                // Convert Assimp.Vector3D to OpenTK.Mathematics.Vector3
                OpenTK.Mathematics.Vector3 opentkPos = new OpenTK.Mathematics.Vector3(pos.X, pos.Y, pos.Z);

                // Apply correction rotation to the vertex position using Quaternion.Transform
                opentkPos = OpenTK.Mathematics.Vector3.Transform(opentkPos, correctionRotation);

                var uv = mesh.HasTextureCoords(0) ? mesh.TextureCoordinateChannels[0][i] : new Assimp.Vector3D(0, 0, 0); // Corrected namespace for Assimp.Vector3D
                vertices.AddRange([opentkPos.X, opentkPos.Y, opentkPos.Z, uv.X, uv.Y]);
            }


            foreach (var face in mesh.Faces)
                indices.AddRange(face.Indices.Select(i => (uint)i));

            _indexCount = indices.Count;

            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
            _ebo = GL.GenBuffer();

            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Count * sizeof(float), vertices.ToArray(), BufferUsageHint.StaticDraw);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Count * sizeof(uint), indices.ToArray(), BufferUsageHint.StaticDraw);

            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float), 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);
            GL.BindVertexArray(0);

            _meshCache[path] = (_vao, _vbo, _ebo, _indexCount);

            _texture = GetOrLoadTexture(path);
            return true;
        }

        private int GetOrLoadTexture(string path)
        {
            if (_textureCache.TryGetValue(path, out var texId))
                return texId;


            var assimpContext = new AssimpContext();
            var importFile = assimpContext.ImportFile(path);
            var mat = importFile.Materials.FirstOrDefault();
            string? texPath = mat?.TextureDiffuse.FilePath;


            if (texPath != null && File.Exists(texPath))
            {
                using var s = File.OpenRead(texPath);
                texId = LoadTexture(ImageResult.FromStream(s, ColorComponents.RedGreenBlueAlpha));
            }
            else if (mat?.HasTextureDiffuse ?? false)
            {
                var t = ImageResult.FromMemory(importFile.Textures.First().CompressedData, ColorComponents.RedGreenBlueAlpha);
                texId = LoadTexture(t);
            }
            else
            {
                texId = CreateMissingTexture();
            }
            assimpContext.Dispose();
            _textureCache[path] = texId;
            return texId;
        }

        private int LoadTexture(ImageResult img)
        {
            int texID = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, texID);

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                img.Width, img.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, img.Data);

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

            byte[] r =
            {
                (byte)Random.Shared.Next(255), (byte)Random.Shared.Next(255), (byte)Random.Shared.Next(255), 255
            };

            byte[] purple = { 255, 0, 255, 255 };
            byte[] black = { 0, 0, 0, 255 };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isPurple = (x + y) % 2 == 0;
                    byte[] color = r;// isPurple ? purple : black;

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
            if (_shaderInitialized) return;

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

            _sharedShader = GL.CreateProgram();
            GL.AttachShader(_sharedShader, vs);
            GL.AttachShader(_sharedShader, fs);
            GL.LinkProgram(_sharedShader);

            GL.DeleteShader(vs);
            GL.DeleteShader(fs);

            _shaderInitialized = true;
        }


        // Предполагается, что q — нормированный
        Quaternion InvertQuaternion(Quaternion q)
        {
            return new Quaternion(-q.X, -q.Y, -q.Z, q.W);
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
            GL.DeleteProgram(_sharedShader);
        }
    }

}
