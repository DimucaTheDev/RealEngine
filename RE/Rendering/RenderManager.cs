using BulletSharp;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core;
using RE.Core.Assets;
using RE.Core.Ui;
using RE.Core.Ui.Debug;
using RE.Core.World;
using RE.Core.World.Components.Physics;
using RE.Core.World.Physics;
using RE.Editor;
using RE.Utils;
using Plane = System.Numerics.Plane;
using Quaternion = OpenTK.Mathematics.Quaternion;
using Vector3 = OpenTK.Mathematics.Vector3;
using Vector4 = OpenTK.Mathematics.Vector4;

namespace RE.Rendering;

//todo перепиши весь класс 🙏😭

public class RenderManager
{
    public static readonly List<Component> RenderingComponents = [];
    
    internal static ShaderProgram OitShaderProgram;
    internal static int FullscreenVao;
    
    private static readonly List<Renderable> Renderables = new();
    private static readonly Plane[] FrustumPlanes = new Plane[6];
    private static readonly LineRenderer FrustumRenderer = new();
    private static bool _hasCameraFrustum;
    private static Matrix4 _cachedViewMatrix, _cachedProjMatrix;

    public static void Init()
    {
        FrustumRenderer.StartRender();
        OitShaderProgram = new ShaderProgram();
        OitShaderProgram.AttachShader("Assets/Shaders/oit.frag");
        OitShaderProgram.AttachShader("Assets/Shaders/oit.vert");
        GL.GenVertexArrays(1, out FullscreenVao);
    }

    public static void AddRenderable<T>(T renderable) where T : Renderable => Renderables.Add(renderable);

    public static void RemoveRenderable<T>(T renderable) where T : Renderable
    {
        Renderables.Remove(renderable);
    }

    public static void RenderAll(FrameEventArgs args)
    {
        GenerateFrustum();

        if (!SceneEditor.Enabled && SceneManager.CurrentScene != null!)
        {
            FrameProfiler.Begin("components");

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthMask(true);
            GL.Disable(EnableCap.Blend);

            foreach (var s in RenderingComponents.Where(s => s is { IsOpaque: true, IsEnabled: true }))
            {
                if (SceneManager.SceneChanged)
                {
                    SceneManager.SceneChanged = false;
                    //uncomment line below if components crash after switching scenes.
                    //return;
                }

                FrameProfiler.Begin(s.GetType().Name);
                s.Render(args);
                FrameProfiler.End();
            }

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, Game.Instance.OitFbo);

            int width = Game.Instance.ClientSize.X;
            int height = Game.Instance.ClientSize.Y;

            GL.BlitFramebuffer(
                0, 0, width, height,
                0, 0, width, height,
                ClearBufferMask.DepthBufferBit,
                BlitFramebufferFilter.Nearest
            );

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, Game.Instance.OitFbo);

            float[] clearZero = [0.0f, 0.0f, 0.0f, 0.0f];

            GL.ClearBuffer(ClearBuffer.Color, 0, clearZero);
            GL.ClearBuffer(ClearBuffer.Color, 1, clearZero);

            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less);
            GL.DepthMask(false);

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(0, BlendingFactorSrc.One, BlendingFactorDest.One);
            GL.BlendFunc(1, BlendingFactorSrc.One, BlendingFactorDest.One);


            foreach (var s in RenderingComponents.Where(s => s is { IsOpaque: false, IsEnabled: true }))
            {
                if (SceneManager.SceneChanged)
                {
                    SceneManager.SceneChanged = false;
                    return;
                }

                FrameProfiler.Begin(s.GetType().Name);
                s.Render(args);
                FrameProfiler.End();
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            GL.DepthMask(true);
            GL.Disable(EnableCap.DepthTest);

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            OitShaderProgram.Use();
            OitShaderProgram.SetValue("accumColorTex", false);
            OitShaderProgram.SetValue("accumWeightTex", true);
            
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, Game.Instance.AccumColorTex);

            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, Game.Instance.AccumWeightTex);

            GL.BindVertexArray(FullscreenVao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);

            GL.Enable(EnableCap.DepthTest);

            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, 0);

            FrameProfiler.End();


            FrameProfiler.Begin("hud");
            Hud.Render();
            FrameProfiler.End();
        }


        foreach (var renderable in Renderables)
        {
            if (renderable.IsVisible)
            {
                if (SceneManager.SceneChanged)
                {
                    SceneManager.SceneChanged = false;
                    return;
                }

                renderable.Render(args);
            }
        }
    }

 
    private static void GenerateFrustum()
    {
        Matrix4 view = Camera.GetActiveCamera().GetViewMatrix();
        Matrix4 proj = Camera.GetActiveCamera().GetProjectionMatrix();
        Matrix4 vp = view * proj;

        FrustumPlanes[0] = new Plane( // Left
            vp.M14 + vp.M11,
            vp.M24 + vp.M21,
            vp.M34 + vp.M31,
            vp.M44 + vp.M41);
        FrustumPlanes[1] = new Plane( // Right
            vp.M14 - vp.M11,
            vp.M24 - vp.M21,
            vp.M34 - vp.M31,
            vp.M44 - vp.M41);
        FrustumPlanes[2] = new Plane( // Bottom
            vp.M14 + vp.M12,
            vp.M24 + vp.M22,
            vp.M34 + vp.M32,
            vp.M44 + vp.M42);
        FrustumPlanes[3] = new Plane( // Top
            vp.M14 - vp.M12,
            vp.M24 - vp.M22,
            vp.M34 - vp.M32,
            vp.M44 - vp.M42);
        FrustumPlanes[4] = new Plane( // Near
            vp.M13,
            vp.M23,
            vp.M33,
            vp.M43);
        FrustumPlanes[5] = new Plane( // Far
            vp.M14 - vp.M13,
            vp.M24 - vp.M23,
            vp.M34 - vp.M33,
            vp.M44 - vp.M43);

        for (int i = 0; i < 6; i++)
        {
            float length = FrustumPlanes[i].Normal.Length();
            FrustumPlanes[i].Normal /= length;
            FrustumPlanes[i].D /= length;
        }
    }

    //todo: move somewhere...
    public static void CreateCameraFrustum()
    {
        _hasCameraFrustum = true;
        _cachedViewMatrix = Camera.Main.GetViewMatrix();
        _cachedProjMatrix = Camera.Main.GetProjectionMatrix();
        FrustumRenderer.Clear();

        float r = .5f;

        var corners = GetFrustumCorners(_cachedProjMatrix, _cachedViewMatrix);
        FrustumRenderer.AddLine(corners[0], corners[1], new Vector4(r, 0, 0, 1), new Vector4(r, 0, 0, 1), false, 0);
        FrustumRenderer.AddLine(corners[1], corners[2], new Vector4(r, 0, 0, 1), new Vector4(r, 0, 0, 1), false, 0);
        FrustumRenderer.AddLine(corners[2], corners[3], new Vector4(r, 0, 0, 1), new Vector4(r, 0, 0, 1), false, 0);
        FrustumRenderer.AddLine(corners[3], corners[0], new Vector4(r, 0, 0, 1), new Vector4(r, 0, 0, 1), false, 0);
        FrustumRenderer.AddLine(corners[4], corners[5], new Vector4(r, 0, 0, 1), new Vector4(r, 0, 0, 1), false, 0);
        FrustumRenderer.AddLine(corners[5], corners[6], new Vector4(r, 0, 0, 1), new Vector4(r, 0, 0, 1), false, 0);
        FrustumRenderer.AddLine(corners[6], corners[7], new Vector4(r, 0, 0, 1), new Vector4(r, 0, 0, 1), false, 0);
        FrustumRenderer.AddLine(corners[7], corners[4], new Vector4(r, 0, 0, 1), new Vector4(r, 0, 0, 1), false, 0);

        for (int i = 0; i < 4; i++)
        {
            FrustumRenderer.AddLine(corners[i], corners[i + 4], new Vector4(r, 0, 0, 1), new Vector4(r, 0, 0, 1),
                false,
                0);
        }
    }

    public static void DrawCameraFrustum()
    {
        _cachedViewMatrix = Camera.Main.GetViewMatrix();
        _cachedProjMatrix = Camera.Main.GetProjectionMatrix();

        Vector4 color = (0.3f, 0.3f, 0.3f, 1);

        var corners = GetFrustumCorners(_cachedProjMatrix, _cachedViewMatrix, 1.3f);
        LineRenderer.DrawLine(corners[0], corners[1], color, color);
        LineRenderer.DrawLine(corners[1], corners[2], color, color);
        LineRenderer.DrawLine(corners[2], corners[3], color, color);
        LineRenderer.DrawLine(corners[3], corners[0], color, color);
        LineRenderer.DrawLine(corners[4], corners[5], color, color);
        LineRenderer.DrawLine(corners[5], corners[6], color, color);
        LineRenderer.DrawLine(corners[6], corners[7], color, color);
        LineRenderer.DrawLine(corners[7], corners[4], color, color);

        for (int i = 0; i < 4; i++)
        {
            LineRenderer.DrawLine(corners[i], corners[i + 4], color, color);
        }
    }

    public static void RemoveCameraFrustum()
    {
        FrustumRenderer.Clear();
        _hasCameraFrustum = false;
    }

    private static Vector3[] GetFrustumCorners(Matrix4 proj, Matrix4 view, float maxDistance = 1000f)
    {
        Matrix4 inv = Matrix4.Invert(view * proj);
        Vector3 camPos = Camera.Main.Position;

        Vector3[] ndc =
        {
            new(-1, -1, -1),
            new(1, -1, -1),
            new(1, 1, -1),
            new(-1, 1, -1),

            new(-1, -1, 1),
            new(1, -1, 1),
            new(1, 1, 1),
            new(-1, 1, 1),
        };

        Vector3[] corners = new Vector3[8];

        for (int i = 0; i < 8; i++)
        {
            Vector4 clip = new Vector4(ndc[i], 1f);
            Vector4 world = Vector4.TransformRow(clip, inv);
            world /= world.W;

            Vector3 pos = world.Xyz;

            Vector3 dir = pos - camPos;
            float dist = dir.Length;

            if (dist > maxDistance)
            {
                pos = camPos + dir.Normalized() * maxDistance;
            }

            corners[i] = pos;
        }

        return corners;
    }
}