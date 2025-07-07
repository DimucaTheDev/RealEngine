using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using RE.Rendering;
using Serilog;
using SixLabors.ImageSharp.Processing;

namespace RE.Core.World.Components
{
    internal class SkyboxComponent(string path) : Component
    {
        private static int _vao, _vbo, _handle;
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
        private static string[] faces =
        [
            "/right.png",   // GL_TEXTURE_CUBE_MAP_POSITIVE_X
            "/left.png",    // GL_TEXTURE_CUBE_MAP_NEGATIVE_X
            "/top.png",     // GL_TEXTURE_CUBE_MAP_POSITIVE_Y
            "/bottom.png",  // GL_TEXTURE_CUBE_MAP_NEGATIVE_Y
            "/front.png",   // GL_TEXTURE_CUBE_MAP_POSITIVE_Z
            "/back.png"     // GL_TEXTURE_CUBE_MAP_NEGATIVE_Z
        ];

        private string path = path;

        public override void Start()
        {
            var vertexSource = File.ReadAllText("Assets/shaders/skybox.vert");
            var fragmentSource = File.ReadAllText("Assets/shaders/skybox.frag");

            var vertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertexShader, vertexSource);
            GL.CompileShader(vertexShader);

            var fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragmentShader, fragmentSource);
            GL.CompileShader(fragmentShader);

            _handle = GL.CreateProgram();
            GL.AttachShader(_handle, vertexShader);
            GL.AttachShader(_handle, fragmentShader);
            GL.LinkProgram(_handle);

            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);

            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();

            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, _cubeVertices.Length * sizeof(float), _cubeVertices,
                BufferUsageHint.StaticDraw);

            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            _cubemap = GL.GenTexture();
            GL.BindTexture(TextureTarget.TextureCubeMap, _cubemap);

            try
            {
                for (int i = 0; i < faces.Length; i++)
                {
                    var pathToFace = path + faces[i];

                    if (File.Exists(pathToFace))
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

        public override void Render(FrameEventArgs args)
        {
            GL.DepthMask(false); // не пишем в z-buffer

            GL.UseProgram(_handle);
            var view = Camera.Instance.GetViewMatrix();
            var proj = Camera.Instance.GetProjectionMatrix();
            view.Row3.X = 0;
            view.Row3.Y = 0;
            view.Row3.Z = 0;
            GL.UniformMatrix4(GL.GetUniformLocation(_handle, "view"), false, ref view);
            GL.UniformMatrix4(GL.GetUniformLocation(_handle, "projection"), false, ref proj); // added ref

            GL.BindVertexArray(_vao);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.TextureCubeMap, _cubemap);
            GL.Uniform1(GL.GetUniformLocation(_handle, "skybox"), 0);


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
    }
}
