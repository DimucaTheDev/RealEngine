using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using RE.Core;

namespace RE.Rendering.Text;

internal class TextRenderer
{
    private static int shaderProgram;

    public static void Init()
    {
        var vertexShader = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vertexShader, File.ReadAllText("assets/shaders/text.vert"));
        GL.CompileShader(vertexShader);

        GL.GetShader(vertexShader, ShaderParameter.CompileStatus, out var vStatus);
        if (vStatus != (int)All.True)
        {
            var infoLog = GL.GetShaderInfoLog(vertexShader);
            throw new Exception($"Ошибка компиляции вершинного шейдера: {infoLog}");
        }

        var fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(fragmentShader, File.ReadAllText("assets/shaders/text.frag"));
        GL.CompileShader(fragmentShader);

        GL.GetShader(fragmentShader, ShaderParameter.CompileStatus, out var fStatus);
        if (fStatus != (int)All.True)
        {
            var infoLog = GL.GetShaderInfoLog(fragmentShader);
            throw new Exception($"Ошибка компиляции фрагментного шейдера: {infoLog}");
        }

        shaderProgram = GL.CreateProgram();
        GL.AttachShader(shaderProgram, vertexShader);
        GL.AttachShader(shaderProgram, fragmentShader);
        GL.LinkProgram(shaderProgram);

        GL.DetachShader(shaderProgram, vertexShader);
        GL.DetachShader(shaderProgram, fragmentShader);
        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);


        RenderLayerManager.SetRenderableInitAction<Text>(() =>
        {
            var projectionM = Matrix4.CreateOrthographicOffCenter(0.0f, Game.Instance.ClientSize.X,
                Game.Instance.ClientSize.Y, 0.0f, -1.0f, 1.0f);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(0, BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
            GL.UseProgram(shaderProgram);
            GL.UniformMatrix4(1, false, ref projectionM);
        });
    }
}