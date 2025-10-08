using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using RE.Core.Assets;
using Serilog;
using SixLabors.ImageSharp.Processing;

namespace RE.Rendering.Renderables;

internal class SkyboxRenderer : Renderable
{
    private static int _vao, _vbo;
    private static ShaderProgram _handle = null!;

    private static readonly float[] _cubeVertices =
    [
        -1, 1, -1, -1, -1, -1, 1, -1, -1,
        1, -1, -1, 1, 1, -1, -1, 1, -1,
        -1, -1, 1, -1, -1, -1, -1, 1, -1,
        -1, 1, -1, -1, 1, 1, -1, -1, 1,
        1, -1, -1, 1, -1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, -1, 1, -1, -1,
        -1, -1, 1, -1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, -1, 1, -1, -1, 1,
        -1, 1, -1, 1, 1, -1, 1, 1, 1,
        1, 1, 1, -1, 1, 1, -1, 1, -1,
        -1, -1, -1, -1, -1, 1, 1, -1, 1,
        1, -1, 1, 1, -1, -1, -1, -1, -1
    ];
    private static int _cubemap;

    private static string[] faces =
    [
        "Assets/skybox/right.png",   // GL_TEXTURE_CUBE_MAP_POSITIVE_X
        "Assets/skybox/left.png",    // GL_TEXTURE_CUBE_MAP_NEGATIVE_X
        "Assets/skybox/top.png",     // GL_TEXTURE_CUBE_MAP_POSITIVE_Y
        "Assets/skybox/bottom.png",  // GL_TEXTURE_CUBE_MAP_NEGATIVE_Y
        "Assets/skybox/front.png",   // GL_TEXTURE_CUBE_MAP_POSITIVE_Z
        "Assets/skybox/back.png"     // GL_TEXTURE_CUBE_MAP_NEGATIVE_Z
    ];

    public static SkyboxRenderer Instance { get; private set; } = null!;

    public override RenderLayer RenderLayer => RenderLayer.Skybox;
    public override bool IsVisible { get; set; } = true;

    public override void Render(FrameEventArgs args)
    {
        GL.DepthMask(false);

        _handle.Use();
        var view = Camera.Instance.GetViewMatrix();
        var proj = Camera.Instance.GetProjectionMatrix();
        view.Row3.X = 0;
        view.Row3.Y = 0;
        view.Row3.Z = 0;
        _handle.SetValue("view", view);
        _handle.SetValue("projection", proj);

        GL.BindVertexArray(_vao);

        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.TextureCubeMap, _cubemap);
        _handle.SetValue("skybox", 0);

        GL.DrawArrays(PrimitiveType.Triangles, 0, 36);

        GL.DepthMask(true);
    }

    public static void Init()
    {
        Instance = new SkyboxRenderer(); // added instance initialization

        _handle = new();
        _handle.AttachShader("Assets/shaders/skybox.vert");
        _handle.AttachShader("Assets/shaders/skybox.frag");

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
                using var image = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(faces[i]);
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

        RenderManager.AddRenderable(Instance);
    }
}