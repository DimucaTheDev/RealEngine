using System.Diagnostics;
using System.Globalization;
using Assimp;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Rendering.Renderables.ModelFormat;
using RE.Rendering.Text;
using RE.Utils;
using Serilog;
using StbImageSharp;
using PrimitiveType = OpenTK.Graphics.OpenGL4.PrimitiveType;
using Quaternion = OpenTK.Mathematics.Quaternion;

namespace RE.Rendering.Renderables
{
    [DebuggerDisplay("{Name}")]
    public class ModelRenderer : Renderable, ICullable
    {
        private static readonly FreeTypeFont Font = new(32, "Assets/Fonts/consola.ttf");
        private static readonly Dictionary<string, uint> TextureCache = new();
        private static Dictionary<string, (uint vao, uint vbo, uint ebo, int indexCount, List<float> vertices, List<int> indices)> _meshCache = new();
        private static int _sharedShader;
        private static bool _shaderInitialized = false;
        private uint _vao, _vbo, _ebo, _texture;
        private int _indexCount;
        private FloatingText? _noModelText;
        private SpriteRenderer? _noModelSprite;
        private bool _modelLoaded = false;
        private string? _exception;

        public string Path
        {
            get;
            set
            {
                TryLoad(value);
                InitShader();
                field = value;
            }
        }
        public Vector4 OutlineColor { get; set; }
        public bool Outline { get; set; }
        public override RenderLayer RenderLayer => RenderLayer.World;
        public override bool IsVisible { get; set; } = true;
        public Vector3 Position
        {
            get => field;
            set
            {
                if (_noModelSprite != null)
                    _noModelSprite.Position = value;
                if (_noModelText != null)
                    _noModelText.Position = value + new Vector3(0, 0.5f, 0);
                field = value;
            }
        }
        public bool ShouldCull { get; set; } = true;
        public Quaternion Rotation { get; set; }
        public Vector3 Scale { get; set; }
        public Matrix4 RotationMatrix { get; set; }
        public string Name { get; set; }
        public float[]? PhysicsVertices { get; private set; }
        public List<int>? PhysicsIndices { get; private set; }

        public ModelRenderer()
        {
            Position = Vector3.Zero;
            Rotation = Quaternion.Identity;
            Scale = Vector3.One;
            Name = $"0x{Random.Shared.Next():x}";
            InitShader();
        }
        public ModelRenderer(string path, Vector3? pos = null, Quaternion? rot = null, Vector3? scale = null, string? name = null)
        {
            Position = pos ?? Vector3.Zero;
            Rotation = rot ?? Quaternion.Identity;
            Scale = scale ?? Vector3.One;
            Name = name ?? $"0x{Random.Shared.Next():x}";
            Path = path;

            TryLoad(Path);
            InitShader();
        }

        public bool TryLoad(string path)
        {
            if (!(_modelLoaded = LoadModel(path)))
            {
                _noModelSprite = new SpriteRenderer(Position, "Assets/Sprites/Editor/no_model.png");
                _noModelText = new FloatingText($"[{Name}]\n{path}\n{_exception}", Position + new Vector3(0, .5f, 0), Font, true);
                _noModelSprite.Render();
                _noModelText.Render();
                return false;
            }
            return true;
        }

        public override void Render(FrameEventArgs args)
        {

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
            if (Outline)
            {
                GL.Enable(EnableCap.CullFace);
                GL.CullFace(TriangleFace.Front); // отбрасываем передние грани, остаются задние
                GL.Uniform1(GL.GetUniformLocation(_sharedShader, "outline"), 1);
                GL.Uniform4(GL.GetUniformLocation(_sharedShader, "outlineColor"), OutlineColor);
                GL.PolygonMode(TriangleFace.Back,
                    PolygonMode.Fill); //todo: render only back side monocolor. somewhy it doesnt work
            }

            GL.DrawElements(PrimitiveType.Triangles, _indexCount, DrawElementsType.UnsignedInt, 0);
            GL.BindVertexArray(0);
            if (Outline)
            {
                GL.Disable(EnableCap.CullFace);
                GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
                GL.Uniform1(GL.GetUniformLocation(_sharedShader, "outline"), 0);

            }
        }

        public void SetTexture(uint texture)
        {
            _texture = texture;
        }

        public override void AddedToRenderList()
        {
            if (!_modelLoaded)
            {
                _noModelSprite?.Render();
                _noModelText?.Render();
            }
        }

        public override void RemovedFromRenderList()
        {
            if (_noModelSprite?.IsRendering() ?? false)
                _noModelSprite?.StopRender();
            if (_noModelText?.IsRendering() ?? false)
                _noModelText?.StopRender();
        }

        private bool LoadModel(string path)
        {
            var renderVertices = new List<float>();
            var physicsVerticesTemp = new List<float>();
            var indices = new List<uint>();

            if (_meshCache.TryGetValue(path, out var meshData))
            {
                (_vao, _vbo, _ebo, _indexCount, renderVertices, PhysicsIndices) = meshData;
                PhysicsVertices = new float[renderVertices.Count / 5 * 3];
                for (int i = 0, j = 0; i < renderVertices.Count; i += 5, j += 3)
                {
                    PhysicsVertices[j] = renderVertices[i];
                    PhysicsVertices[j + 1] = renderVertices[i + 1];
                    PhysicsVertices[j + 2] = renderVertices[i + 2];
                }

                _texture = GetOrLoadTexture(path);
                return true;
            }

            if (path.EndsWith(".smdl", true, CultureInfo.InvariantCulture))
            {
                var m = StaticModelLoader.TryLoadModel(path, out var data);
                if (m != null)
                {
                    _exception = m.Message;
                    return false;
                }


                Name = data.Name;

                for (int i = 0; i < data.Vertices.Length; i++)
                {
                    var v = data.Vertices[i];

                    v = Vector3.Transform(v, Quaternion.FromAxisAngle(Vector3.UnitX, MathHelper.DegreesToRadians(-90.0f)));

                    renderVertices.Add(v.X);
                    renderVertices.Add(v.Y);
                    renderVertices.Add(v.Z);

                    if (data.UVs != null && i < data.UVs.Length)
                    {
                        renderVertices.Add(data.UVs[i].X);
                        renderVertices.Add(data.UVs[i].Y);
                    }
                    else
                    {
                        renderVertices.Add(0.0f);
                        renderVertices.Add(0.0f);
                    }

                    physicsVerticesTemp.Add(v.X);
                    physicsVerticesTemp.Add(v.Y);
                    physicsVerticesTemp.Add(v.Z);
                }

                foreach (var index in data.Indices)
                {
                    indices.Add((uint)index);
                }

                _indexCount = indices.Count;

                _vao = (uint)GL.GenVertexArray();
                _vbo = (uint)GL.GenBuffer();
                _ebo = (uint)GL.GenBuffer();

                GL.BindVertexArray(_vao);

                GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
                GL.BufferData(BufferTarget.ArrayBuffer, renderVertices.Count * sizeof(float), renderVertices.ToArray(), BufferUsageHint.StaticDraw);

                GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
                GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Count * sizeof(uint), indices.ToArray(), BufferUsageHint.StaticDraw);

                GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), 0);
                GL.EnableVertexAttribArray(0);
                GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float), 3 * sizeof(float));
                GL.EnableVertexAttribArray(1);

                GL.BindVertexArray(0);

                _meshCache[path] = (_vao, _vbo, _ebo, _indexCount, renderVertices, indices.Select(s => (int)s).ToList());

                PhysicsVertices = physicsVerticesTemp.ToArray();
                PhysicsIndices = indices.Select(i => (int)i).ToList();
                _texture = GetOrLoadTexture(path);

                return true;
            }
            else
            {
                using AssimpContext importer = new AssimpContext();
                Scene scene;
                try
                {
                    scene = importer.ImportFile(path,
                        PostProcessSteps.Triangulate | PostProcessSteps.GenerateNormals | PostProcessSteps.FlipUVs);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Failed to load model {Name} at {path}");
                    _exception = ex.Message;
                    return false;
                }

                if (string.IsNullOrEmpty(scene.RootNode.Name))
                    Name = scene.RootNode.Name;

                if (!scene.Meshes.Any())
                    return false;


                var mesh = scene.Meshes[0];

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

                Quaternion correctionRotation =
                    Quaternion.FromAxisAngle(Vector3.UnitX, MathHelper.DegreesToRadians(-90.0f));

                for (int i = 0; i < mesh.VertexCount; i++)
                {
                    var pos = mesh.Vertices[i] - center;
                    Vector3 opentkPos = new Vector3(pos.X, pos.Y, pos.Z);

                    opentkPos = Vector3.Transform(opentkPos, correctionRotation);

                    var uv = mesh.HasTextureCoords(0)
                        ? mesh.TextureCoordinateChannels[0][i]
                        : new Vector3D(0, 0, 0);

                    renderVertices.AddRange([opentkPos.X, opentkPos.Y, opentkPos.Z, uv.X, uv.Y]);

                    physicsVerticesTemp.AddRange([opentkPos.X, opentkPos.Y, opentkPos.Z]);
                }


                foreach (var face in mesh.Faces)
                    indices.AddRange(face.Indices.Select(i => (uint)i));

                _indexCount = indices.Count;

                _vao = (uint)GL.GenVertexArray();
                _vbo = (uint)GL.GenBuffer();
                _ebo = (uint)GL.GenBuffer();

                GL.BindVertexArray(_vao);
                GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
                GL.BufferData(BufferTarget.ArrayBuffer, renderVertices.Count * sizeof(float), renderVertices.ToArray(),
                    BufferUsageHint.StaticDraw);

                GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
                GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Count * sizeof(uint), indices.ToArray(),
                    BufferUsageHint.StaticDraw);

                GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), 0);
                GL.EnableVertexAttribArray(0);
                GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float),
                    3 * sizeof(float));
                GL.EnableVertexAttribArray(1);
                GL.BindVertexArray(0);

                _meshCache[path] = ((uint)_vao, (uint)_vbo, (uint)_ebo, _indexCount, renderVertices,
                    indices.Select(s => (int)s).ToList());

                PhysicsVertices = physicsVerticesTemp.ToArray();
                PhysicsIndices = indices.Select(i => (int)i).ToList();
                _texture = GetOrLoadTexture(path, scene);
                return true;
            }
        }

        private uint GetOrLoadTexture(string path, Scene? scene = null)
        {
            if (TextureCache.TryGetValue(path, out var texId))
                return texId;

            if (path.EndsWith(".smdl", true, CultureInfo.InvariantCulture))
            {
                if (!File.Exists(path + ".png"))
                    return Util.CreateMissingTexture();

                var readAllBytes = File.ReadAllBytes(path + ".png");
                var t = ImageResult.FromMemory(readAllBytes, ColorComponents.RedGreenBlueAlpha);
                var lt = LoadTexture(t);
                TextureCache[path] = lt;

                return lt;
            }

            Scene importFile = scene!;

            if (scene == null)
            {
                using var assimpContext = new AssimpContext();
                importFile = assimpContext.ImportFile(path);
            }


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
                Log.Warning($"No texture for {path}");
                texId = Util.CreateMissingTexture();
            }

            importFile.Clear();

            TextureCache[path] = texId;
            return texId;
        }

        private uint LoadTexture(ImageResult img)
        {
            uint texId = (uint)GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, texId);

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                img.Width, img.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, img.Data);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            return texId;
        }

        private void InitShader()
        {
            if (_shaderInitialized)
                return;

            string vertexShaderSrc = File.ReadAllText("assets/shaders/assimp.vert");
            string fragmentShaderSrc = File.ReadAllText("assets/shaders/assimp.frag");

            int vs = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vs, vertexShaderSrc);
            GL.CompileShader(vs);
            GL.GetShader(vs, ShaderParameter.CompileStatus, out var status);
            if (status == 0)
                throw new Exception(GL.GetShaderInfoLog(vs));

            int fs = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fs, fragmentShaderSrc);
            GL.CompileShader(fs);
            GL.GetShader(fs, ShaderParameter.CompileStatus, out status);
            if (status == 0)
                throw new Exception(GL.GetShaderInfoLog(fs));

            _sharedShader = GL.CreateProgram();
            GL.AttachShader(_sharedShader, vs);
            GL.AttachShader(_sharedShader, fs);
            GL.LinkProgram(_sharedShader);

            GL.DeleteShader(vs);
            GL.DeleteShader(fs);

            _shaderInitialized = true;
        }

        public override void Dispose()
        {
            this.StopRender();
            _noModelText?.StopRender();
            _noModelSprite?.StopRender();
            _noModelText?.Dispose();
            _noModelSprite?.Dispose();
            GL.DeleteVertexArray(_vao);
            GL.DeleteBuffer(_vbo);
            GL.DeleteBuffer(_ebo);
            GL.DeleteTexture(_texture);
            GL.DeleteProgram(_sharedShader);
        }
    }

}
