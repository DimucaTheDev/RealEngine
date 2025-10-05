using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using RE.Core;
using RE.Core.Assets;
using RE.Rendering.Renderables;

namespace RE.Rendering.Text;

internal class TextRenderer
{
    private static ShaderProgram ShaderProgram;

    public static void Init()
    {
        ShaderProgram = new();
        ShaderProgram.AttachShader("assets/shaders/text.frag");
        ShaderProgram.AttachShader("assets/shaders/text.vert");

        RenderManager.SetRenderableInitAction<ScreenText>(() =>
        {
            var projectionM = Matrix4.CreateOrthographicOffCenter(0.0f, Game.Instance.ClientSize.X,
                Game.Instance.ClientSize.Y, 0.0f, -1.0f, 1.0f);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(0, BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
            ShaderProgram.Use(); 
            GL.UniformMatrix4(1, false, ref projectionM);
        });
    }
}