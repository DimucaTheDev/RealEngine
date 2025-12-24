using System.Diagnostics;
using System.Globalization;
using Assimp;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core;
using RE.Core.Assets;
using RE.Core.World;
using RE.Rendering;
using RE.Rendering.Lightning;
using RE.Rendering.Renderables;
using RE.Rendering.Renderables.ModelFormat;
using RE.Rendering.Text;
using RE.Utils;
using Serilog;
using StbImageSharp;
using Camera = RE.Rendering.Camera;
using Material = RE.Rendering.Lightning.Material;
using PrimitiveType = OpenTK.Graphics.OpenGL4.PrimitiveType;
using Quaternion = OpenTK.Mathematics.Quaternion;
using Scene = Assimp.Scene;

namespace RE.Debug.Overlay.Editor.Panels.MaterialPreview
{
    [DebuggerDisplay("{Name}")]
    public class MaterialModelRenderer : Renderable
    {
        private static readonly Dictionary<string, Texture> TextureCache = new();
        private static readonly Dictionary<string, (uint vao, uint vbo, uint ebo, int indexCount, List<float> vertices, List<int> indices)> MeshCache = new();
        private static ShaderProgram _program = null!;
        private static bool _shaderInitialized = false;
        private uint _vao, _vbo, _ebo;
        private Texture _texture;
        private int _indexCount;

        public string Path
        {
            get;
            set
            {
                this.IsVisible = false;
                LoadModel(value);
                InitShader();
                field = value;
            }
        }
        public override RenderLayer RenderLayer => RenderLayer.World;
        public override bool IsVisible { get; set; } = true;

        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
        public Vector3 Scale { get; set; }
        public string Name { get; set; }
        public Material Material { get; set; } = new();
        public bool IgnoreLight { get; set; }

        public MaterialModelRenderer(string path, Vector3? pos = null, Quaternion? rot = null, Vector3? scale = null, string? name = null)
        {
            Position = pos ?? Vector3.Zero;
            Rotation = rot ?? Quaternion.Identity;
            Scale = scale ?? Vector3.One;
            Name = name ?? $"0x{Random.Shared.Next():x}";
            Path = path;

            LoadModel(Path);
            InitShader();
        }


        public override void Render(FrameEventArgs args)
        {
            throw new();
        }

        public void Render(FrameEventArgs args, Matrix4 model, Camera camera)
        {
            Matrix4 view = camera.GetViewMatrix();
            Matrix4 proj = camera.GetProjectionMatrix();

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _texture.AsOpenGl());
            //todo: same for Texture1 and specular map

            _program.Use();
            _program.SetValue("model", model);
            _program.SetValue("view", view);
            _program.SetValue("projection", proj);


            // lighting.glsl
            _program.SetStruct("material", Material);
            if (IgnoreLight)
                _program.SetValue("ignoreLight", true);
            else
            {
                _program.SetValue("ignoreLight", false);
                _program.SetValue("hasSpotLight", false);
                _program.SetValue("hasDirLight", true);
                _program.SetValue("viewPos", camera.Position);
                ILightSource light = new DirectionalLight()
                {
                    Direction = (.3f, -.5f, -.3f),
                    DiffuseColor = Vector3.One,
                    SpecularColor = Vector3.One
                };
                light.SetParams(_program);
            }

            GL.BindVertexArray(_vao);

            GL.DrawElements(PrimitiveType.Triangles, _indexCount, DrawElementsType.UnsignedInt, 0);
            GL.BindVertexArray(0);

            GL.UseProgram(ShaderProgram.NoProgram);

        }

        private static int i = 0;
        public void SetTexture(Texture texture, bool deleteCurrent = false)
        {
            if (deleteCurrent)
                _texture.Delete();
            _texture = texture;
        }

        private void LoadModel(string path)
        {
            var renderVertices = new List<float>();
            var physicsVerticesTemp = new List<float>();
            var indices = new List<uint>();

            if (MeshCache.TryGetValue(path, out var meshData))
            {
                (_vao, _vbo, _ebo, _indexCount, renderVertices, _) = meshData;


                _texture = GetOrLoadTexture(path);
                return;
            }

            if (path.EndsWith(".smdl", true, CultureInfo.InvariantCulture))
            {
                var m = StaticModelLoader.TryLoadModel(path, out var data);
                if (m != null)
                {
                    return;
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

                MeshCache[path] = (_vao, _vbo, _ebo, _indexCount, renderVertices, indices.Select(s => (int)s).ToList());

                _texture = GetOrLoadTexture(path);

                return;
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
                    Log.Error(ex, "Assimp failed to load model {ModelName} at {Path}", Name, path);
                    return;
                }

                if (string.IsNullOrEmpty(scene.RootNode.Name))
                    Name = scene.RootNode.Name;

                if (!scene.Meshes.Any())
                    return;

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
                    indices.Select(s => (int)s).ToList());

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

                _texture = GetOrLoadTexture(path, scene);
                return;
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
                var tex = new Texture(t.Data, t.Width, t.Height);

                TextureCache[path] = tex;

                return tex;
            }

            Scene importFile = scene!;

            if (scene == null)
            {
                using var assimpContext = new AssimpContext();
                importFile = assimpContext.ImportFile(path);
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
            GL.DeleteVertexArray(_vao);
            GL.DeleteBuffer(_vbo);
            GL.DeleteBuffer(_ebo);
            _texture.Delete();
        }
    }
}