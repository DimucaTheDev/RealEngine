using System.Diagnostics;
using System.Globalization;
using Assimp;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core;
using RE.Core.Assets;
using RE.Core.World;
using RE.Rendering.Lightning;
using RE.Rendering.Renderables.ModelFormat;
using RE.Rendering.Text;
using RE.Utils;
using Serilog;
using StbImageSharp;
using Material = RE.Rendering.Lightning.Material;
using PrimitiveType = OpenTK.Graphics.OpenGL4.PrimitiveType;
using Quaternion = OpenTK.Mathematics.Quaternion;
using Scene = Assimp.Scene;
using SceneEditor = RE.Debug.Overlay.Editor.SceneEditor;

namespace RE.Rendering.Renderables
{
    [DebuggerDisplay("{Name}")]
    public class ModelRenderer : Renderable, ICullable
    {
        private static readonly FreeTypeFont Font = new(32, "Assets/Fonts/consola.ttf");
        private static readonly Dictionary<string, Texture> TextureCache = new();
        private static readonly Dictionary<string, (uint vao, uint vbo, uint ebo, int indexCount, List<float> vertices, List<int> indices, Vector3 min, Vector3 max)> MeshCache = new();
        private static ShaderProgram _program = null!;
        private static bool _shaderInitialized = false;
  
        private uint _vao, _vbo, _ebo;
        private int _indexCount;
        private Texture _texture;
        private FloatingText? _noModelText;
        private SpriteRenderer? _noModelSprite;
        private bool _modelLoaded = false;
        private string? _exception;

        public string Path
        {
            get;
            set
            {
                _noModelSprite?.StopRender();
                _noModelText?.StopRender();
                this.IsVisible = false;
                TryLoad(value);
                InitShader();
                field = value;
            }
        }
        public Vector4 OutlineColor { get; set; }
        public bool Outline { get; set; }
        public override RenderLayer RenderLayer => RenderLayer.World;
        public override bool IsVisible { get; set; } = true;
        public Vector3 MinBounds { get; set; }
        public Vector3 MaxBounds { get; set; }

        public Vector3 Position
        {
            get;
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
        public string Name { get; set; }
        public float[]? PhysicsVertices { get; private set; }
        public List<int>? PhysicsIndices { get; private set; }
        public Material Material { get; set; } = new();
        public bool IgnoreLight { get; set; }
        public bool ConstantSize { get; set; }

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
            _noModelSprite?.Dispose();
            _noModelText?.Dispose();
            _noModelSprite = null;
            _noModelText = null;

            if (!(_modelLoaded = LoadModel(path)))
            {
                _noModelSprite = new SpriteRenderer(Position, "Assets/Sprites/Editor/no_model.png");
                _noModelText = new FloatingText($"[{Name}]\n{path}\n{_exception}", Position + new Vector3(0, .5f, 0), Font, true);
                _noModelSprite.StartRender();
                _noModelText.StartRender();
                IsVisible = false;
                return false;
            }

            IsVisible = true;
            return true;
        }

        public override void Render(FrameEventArgs args)
        {
            var viewPos = Camera.GetActiveCamera().Position;
            float distance = (viewPos - Position).Length;

            const float baseScaleFactor = 0.075f;
            float constantScale = distance * baseScaleFactor;

            Matrix4 model =
                Matrix4.CreateScale(ConstantSize ? new(constantScale) : Scale) *
                Matrix4.CreateFromQuaternion(Rotation) *
                Matrix4.CreateTranslation(Position);
            if (IsVisible)
                Render(args, model, Camera.GetActiveCamera());
        }

        public void Render(FrameEventArgs args, Matrix4 model, Camera camera)
        {
            Matrix4 view = camera.GetViewMatrix();
            Matrix4 proj = camera.GetProjectionMatrix();

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _texture.AsOpenGl());
            //todo: same for Texture1 and specular map

            if (Outline)
            {
                GL.Enable(EnableCap.CullFace);
                GL.CullFace(TriangleFace.Front);
                //GL.PolygonMode(TriangleFace.Back, PolygonMode.Fill); //todo: render only back side monocolor. somewhy it doesnt work
            }

            _program.Use();
            _program.SetValue("model", model);
            _program.SetValue("view", view);
            _program.SetValue("projection", proj);
            if (Outline)
            {
                _program.SetValue("outline", 1);
                _program.SetValue("outlineColor", OutlineColor with { W = (MathF.Sin(Time.ElapsedTime * 4) / 2 + 0.5f) });
            }


            // lighting.glsl
            _program.SetStruct("material", Material);
            if (IgnoreLight || (SceneEditor.Enabled && !SceneEditor.PreviewLight))
                _program.SetValue("ignoreLight", true);
            else
            {
                var lights = SceneManager.CurrentScene.LightSources;

                _program.SetValue("ignoreLight", false);
                _program.SetValue("hasSpotLight", lights.Any(s => s is SpotLight));
                _program.SetValue("hasDirLight", lights.Any(s => s is DirectionalLight));
                _program.SetValue("viewPos", Camera.GetActiveCamera().Position);

                foreach (var light in lights)
                {
                    light.SetParams(_program);
                }
                /*_program.SetStruct("spotLight", new SpotLight()
                {
                    Position = Camera.Main.Position,
                    Direction = Camera.Main.Front,
                    DiffuseColor = Vector3.One,
                    SpecularColor = Vector3.One,
                    Constant = 1.0f,
                    Linear = 0.09f,
                    Quadratic = 0.032f,
                    CutOff = MathF.Cos(MathHelper.DegreesToRadians(12.5f)),
                    OuterCutOff = MathF.Cos(MathHelper.DegreesToRadians(17.5f))
                });*/
            }

            GL.BindVertexArray(_vao);

            GL.DrawElements(PrimitiveType.Triangles, _indexCount, DrawElementsType.UnsignedInt, 0);
            GL.BindVertexArray(0);

            if (Outline)
                _program.SetValue("outline", 0);

            GL.UseProgram(ShaderProgram.NoProgram);

            if (Outline)
            {
                // GL_INVALID_ENUM error generated. Polygon modes for <face> are disabled in the current profile.
                GL.Disable(EnableCap.CullFace);
                //GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
            }
            // LineRenderer.DrawLine(Position + (0, 2, 0) + dirLightDirection, Position + (0, 2, 0), new(1, 0, 0, 1), new(0, 0, 1, 1));
        }
         
        public void SetTexture(Texture texture, bool deleteCurrent = false)
        {
            if (deleteCurrent)
                _texture.Delete();
            _texture = texture;
        }

        public override void AddedToRenderList()
        {
            if (!_modelLoaded)
            {
                _noModelSprite?.StartRender();
                _noModelText?.StartRender();
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

            if (MeshCache.TryGetValue(path, out var meshData))
            {
                (_vao, _vbo, _ebo, _indexCount, renderVertices, PhysicsIndices, MinBounds, MaxBounds) = meshData;
                PhysicsVertices = new float[renderVertices.Count / 8 * 3];
                for (int i = 0, j = 0; i < renderVertices.Count; i += 8, j += 3)
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

                var min = new Vector3D(float.MaxValue, float.MaxValue, float.MaxValue);
                var max = new Vector3D(float.MinValue, float.MinValue, float.MinValue);
                for (int i = 0; i < data.Vertices.Length; i++)
                {
                    var v = data.Vertices[i];

                    v = Vector3.Transform(v, Quaternion.FromAxisAngle(Vector3.UnitX, MathHelper.DegreesToRadians(-90.0f)));

                    min.X = Math.Min(min.X, v.X);
                    min.Y = Math.Min(min.Y, v.Y);
                    min.Z = Math.Min(min.Z, v.Z);

                    max.X = Math.Max(max.X, v.X);
                    max.Y = Math.Max(max.Y, v.Y);
                    max.Z = Math.Max(max.Z, v.Z);

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

                    if (data.Normals != null && i < data.Normals.Length)
                    {
                        Vector3 rv = new(data.Normals[i].X, data.Normals[i].Y, data.Normals[i].Z);

                        rv = Vector3.Transform(rv, Quaternion.FromAxisAngle(Vector3.UnitX, MathHelper.DegreesToRadians(-90.0f)));

                        renderVertices.AddRange(rv.X, rv.Y, rv.Z);
                    }
                    else
                    {
                        renderVertices.Add(0.0f);
                        renderVertices.Add(0.0f);
                        renderVertices.Add(0.0f);
                    }

                    physicsVerticesTemp.Add(v.X);
                    physicsVerticesTemp.Add(v.Y);
                    physicsVerticesTemp.Add(v.Z);
                }

                MaxBounds = max.ToOpenTkVector3();
                MinBounds = min.ToOpenTkVector3();

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


                // pos
                GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 0);
                GL.EnableVertexAttribArray(0);

                // uv
                GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 8 * sizeof(float), 3 * sizeof(float));
                GL.EnableVertexAttribArray(1);

                // normals
                GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 5 * sizeof(float));
                GL.EnableVertexAttribArray(2);

                GL.BindVertexArray(0);

                MeshCache[path] = (_vao, _vbo, _ebo, _indexCount, renderVertices, indices.Select(s => (int)s).ToList(), MinBounds, MaxBounds);

                PhysicsVertices = physicsVerticesTemp.ToArray();
                PhysicsIndices = indices.Select(indie => (int)indie).ToList();
                _texture = GetOrLoadTexture(path);

                return true;
            }
            else
            {
                using AssimpContext importer = new AssimpContext();
                Scene scene;
                try
                {
                    scene = importer.ImportFileFromStream(ContentManager.Open(path),
                        PostProcessSteps.Triangulate | PostProcessSteps.GenerateNormals | PostProcessSteps.FlipUVs);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Assimp failed to load model {ModelName} at {Path}", Name, path);
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

                MaxBounds = max.ToOpenTkVector3();
                MinBounds = min.ToOpenTkVector3();

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

                    var normal = mesh.HasNormals ? mesh.Normals[i].ToOpenTkVector3() : new(0, 0, 1);

                    normal = Vector3.Transform(normal, Quaternion.FromAxisAngle(Vector3.UnitX, MathHelper.DegreesToRadians(-90.0f)));

                    renderVertices.AddRange([opentkPos.X, opentkPos.Y, opentkPos.Z, uv.X, uv.Y, normal.X, normal.Y, normal.Z]);

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

                // pos
                GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 0);
                GL.EnableVertexAttribArray(0);

                // uv
                GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 8 * sizeof(float), 3 * sizeof(float));
                GL.EnableVertexAttribArray(1);

                // normals
                GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 5 * sizeof(float));
                GL.EnableVertexAttribArray(2);


                MeshCache[path] = ((uint)_vao, (uint)_vbo, (uint)_ebo, _indexCount, renderVertices,
                    indices.Select(s => (int)s).ToList(), MinBounds, MaxBounds);

                var mat = scene.Materials[mesh.MaterialIndex];
                if (mat != null)
                {
                    Material = new Material
                    {
                        Shininess = mat.Shininess
                    };
                }
                else
                    Material = new Material();

                PhysicsVertices = physicsVerticesTemp.ToArray();
                PhysicsIndices = indices.Select(i => (int)i).ToList();
                _texture = GetOrLoadTexture(path, scene);
                return true;
            }
        }

        private Texture GetOrLoadTexture(string path, Scene? scene = null)
        {
            if (TextureCache.TryGetValue(path, out var texId))
                return texId;

            if (path.EndsWith(".smdl", true, CultureInfo.InvariantCulture))
            {
                if (!ContentManager.Exists(path + ".png"))
                    return Texture.CreateMissingTexture();

                var readAllBytes = ContentManager.GetBytes(path + ".png");
                var t = ImageResult.FromMemory(readAllBytes, ColorComponents.RedGreenBlueAlpha);
                var lt = new Texture(t.Data, t.Width, t.Height);
                TextureCache[path] = lt;

                return lt;
            }

            Scene importFile = scene!;

            if (scene == null)
            {
                using var assimpContext = new AssimpContext();
                importFile = assimpContext.ImportFileFromStream(ContentManager.Open(path));
            }


            var mat = importFile.Materials.FirstOrDefault();
            string? texPath = mat?.TextureDiffuse.FilePath;


            if (texPath != null && ContentManager.Exists(texPath))
            {
                texId = new Texture(texPath);
            }
            else if (mat?.HasTextureDiffuse ?? false)
            {
                var t = ImageResult.FromMemory(importFile.Textures.First().CompressedData, ColorComponents.RedGreenBlueAlpha);
                texId = new Texture(t.Data, t.Width, t.Height);
            }
            else
            {
                Log.Warning("No texture for {Path}", path);
                texId = Texture.CreateMissingTexture();
            }

            importFile.Clear();

            TextureCache[path] = texId;
            return texId;
        }

        private void InitShader()
        {
            if (_shaderInitialized)
                return;

            _program = new();
            _program.AttachShader("assets/shaders/assimp.vert");
            _program.AttachShader("assets/shaders/assimp.frag");

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
            _texture.Delete();
        }
    }
}