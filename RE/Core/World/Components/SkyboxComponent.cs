using System.Text.Json.Nodes;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using RE.Core.Assets;
using RE.Core.Scripting;
using RE.Debug;
using RE.Rendering;
using RE.Rendering.Renderables;
using Serilog;
using SixLabors.ImageSharp.Processing;
using SceneEditor = RE.Debug.Overlay.Editor.SceneEditor;
using Vector3 = OpenTK.Mathematics.Vector3;

namespace RE.Core.World.Components
{
    [ComponentInfo("World", Description = $"Renders Skybox with 6 images located in '{nameof(Path)}':\r\n/right.png\r\n/left.png\r\n/top.png\r\n/bottom.png\r\n/front.png\r\n/back.png\r\n")]
    internal class SkyboxComponent(string path) : Component, IDebugRenderer
    {
        private ShaderProgram _program = null!;
        private SpriteRenderer _sprite = new(Vector3.Zero, "assets/sprites/editor/skybox.png", scale: 0.5f);

        private static int _vao, _vbo;
        private static readonly float[] _cubeVertices =
        [
            -1, 1, -1, -1, -1, -1, 1, -1, -1,
            1, -1, -1, 1, 1, -1, -1, 1, -1, // задняя
            -1, -1, 1, -1, -1, -1, -1, 1, -1,
            -1, 1, -1, -1, 1, 1, -1, -1, 1, // левая
            1, -1, -1, 1, -1, 1, 1, 1, 1,
            1, 1, 1, 1, 1, -1, 1, -1, -1, // правая
            -1, -1, 1, -1, 1, 1, 1, 1, 1,
            1, 1, 1, 1, -1, 1, -1, -1, 1, // передняя
            -1, 1, -1, 1, 1, -1, 1, 1, 1,
            1, 1, 1, -1, 1, 1, -1, 1, -1, // верх
            -1, -1, -1, -1, -1, 1, 1, -1, 1,
            1, -1, 1, 1, -1, -1, -1, -1, -1 // низ
        ];
        private static int _cubemap;
        private static string[] _faces =
        [
            "/right.png",   // GL_TEXTURE_CUBE_MAP_POSITIVE_X
            "/left.png",    // GL_TEXTURE_CUBE_MAP_NEGATIVE_X
            "/top.png",     // GL_TEXTURE_CUBE_MAP_POSITIVE_Y
            "/bottom.png",  // GL_TEXTURE_CUBE_MAP_NEGATIVE_Y
            "/front.png",   // GL_TEXTURE_CUBE_MAP_POSITIVE_Z
            "/back.png"     // GL_TEXTURE_CUBE_MAP_NEGATIVE_Z
        ];

        [EditorProperty("SkyBox Path")]
        public string Path
        {
            get;
            set
            {
                field = value;
                LoadSkybox();
            }
        } = path;

        public SkyboxComponent() : this("") { }

        private void LoadSkybox()
        {
            GL.DeleteTexture(_cubemap);
            _cubemap = GL.GenTexture();
            GL.BindTexture(TextureTarget.TextureCubeMap, _cubemap);

            try
            {
                for (int i = 0; i < _faces.Length; i++)
                {
                    var pathToFace = Path + _faces[i];
                    if (ContentManager.Exists(pathToFace))
                    {
                        using var image =
                            SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(pathToFace);
                        image.Mutate(x => x.Flip(FlipMode.Horizontal)); // OpenGL flip
                        var pixels = new byte[4 * image.Width * image.Height];
                        image.CopyPixelDataTo(pixels);

                        GL.TexImage2D(TextureTarget.TextureCubeMapPositiveX + i, 0,
                            PixelInternalFormat.Rgba,
                            image.Width, image.Height, 0,
                            PixelFormat.Rgba,
                            PixelType.UnsignedByte,
                            pixels);
                    }
                    else
                    {
                        Log.Error("Could not load texture for face {FaceIndex} at path: {Path}", i, pathToFace);

                        var p = CreateMissingTexture();
                        GL.TexImage2D(TextureTarget.TextureCubeMapPositiveX + i, 0,
                            PixelInternalFormat.Rgba,
                            100, 100, 0,
                            PixelFormat.Rgba,
                            PixelType.UnsignedByte,
                            p);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Unable to load panorama");
            }

            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);
        }

        public override void Start()
        {
            _program = new ShaderProgram();

            _program.AttachShader("Assets/shaders/skybox.vert");
            _program.AttachShader("Assets/shaders/skybox.frag");

            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();

            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, _cubeVertices.Length * sizeof(float), _cubeVertices,
                BufferUsageHint.StaticDraw);

            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            LoadSkybox();
        }

        public override void Render(FrameEventArgs args)
        {
            if (SceneEditor.Enabled && !SceneEditor.PreviewSkybox)
                return;

            GL.DepthMask(false);

            _program.Use();

            var view = Camera.Instance.GetViewMatrix();
            var proj = Camera.Instance.GetProjectionMatrix();

            view.Row3.X = 0;
            view.Row3.Y = 0;
            view.Row3.Z = 0;

            _program.SetValue("view", view);
            _program.SetValue("projection", proj);
            _program.SetValue("skybox", 0);

            GL.BindVertexArray(_vao);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.TextureCubeMap, _cubemap);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 36);

            GL.DepthMask(true);

        }
        private byte[] CreateMissingTexture()
        {
            const int size = 100;

            byte[] data = new byte[size * size * 4];

            byte[] r =
            [
                (byte)Random.Shared.Next(255),
                (byte)Random.Shared.Next(255),
                (byte)Random.Shared.Next(255),
                255
            ];

            byte[] purple = { 255, 0, 255, 255 };
            byte[] black = { 0, 0, 0, 255 };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isPurple = (x + y) % 2 == 0;
                    byte[] color = isPurple ? purple : black;

                    int index = (y * size + x) * 4;
                    System.Buffer.BlockCopy(color, 0, data, index, 4);
                }
            }
            return data;
        }
        public override JsonNode GetSaveData()
        {
            JsonObject root = new();
            JsonArray args =
            [
                Path
            ];
            root.Add("args", args);
            return root;
        }

        public void DebugRender(FrameEventArgs args)
        {
            _sprite.Position = Owner.Transform.Position;
            _sprite.Render(args);
        }
    }
}
