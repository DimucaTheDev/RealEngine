using System.Diagnostics;
using Hexa.NET.ImGui;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core;
using RE.Core.Assets;
using RE.Core.Ui;
using RE.Core.Ui.Debug;
using RE.Core.World;
using RE.Editor;
using RE.Editor.Notification;
using RE.Rendering.Texturing;
using RE.Utils;
using Serilog;
using Vector2 = System.Numerics.Vector2;

namespace RE.Rendering
{
    public static class RenderManager
    {
        private static string resLoc = "";

        internal static ShaderProgram OitShaderProgram;
        internal static int FullscreenVao;
        internal static readonly List<Renderable> Renderables = new();

        public static void Init()
        {
            OitShaderProgram = new ShaderProgram();
            OitShaderProgram.AttachShader("Assets/Shaders/Pass/OitResolve/oit.frag");
            OitShaderProgram.AttachShader("Assets/Shaders/Pass/OitResolve/oit.vert");

            GL.GenVertexArrays(1, out FullscreenVao);
        }

        public static void AddRenderable(Renderable renderable) => Renderables.Add(renderable);

        public static void RemoveRenderable(Renderable renderable) => Renderables.Remove(renderable);

        public static void RenderAll(double args)
        {
            if (!SceneEditor.Enabled && SceneManager.CurrentScene != null!)
            {
                RenderTo(args, Camera.Main);
                PostProcessLayer.Default.Draw((int)Camera.Main.RenderTexture!.AsOpenGl());
            }

            foreach (var renderable in Renderables.Where(renderable => renderable.IsVisible))
            {
                if (SceneManager.SceneChanged)
                {
                    SceneManager.SceneChanged = false;
                    return;
                }

                renderable.Render(args);
            }
        }

        public static void RenderTo(double deltaTime, Camera camera)
        {
            var active = Camera._activeCamera;
            Camera._activeCamera = camera;

            using (FrameProfiler.Scope(camera.Name))
            {
                var ppBuffer = camera.PrePostProcessFramebuffer;

                using (FrameProfiler.Scope("components"))
                {
                    ppBuffer.Bind();
                    GL.ClearColor(Color4.Black);
                    GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                    GL.Enable(EnableCap.DepthTest);
                    GL.DepthMask(true);
                    GL.Disable(EnableCap.Blend);

                    foreach (var s in SceneManager.CurrentScene.RenderingComponents.Where(s =>
                                 s is { IsOpaque: true, IsEnabled: true }))
                    {
                        if (SceneManager.SceneChanged)
                        {
                            SceneManager.SceneChanged = false;
                        }

                        using (FrameProfiler.Scope(s.GetType().Name))
                        {
                            s.Render(deltaTime);
                        }
                    }

                    GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, ppBuffer.Handle);
                    GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, camera.OitFbo);


                    int width = Game.Instance.ClientSize.X;
                    int height = Game.Instance.ClientSize.Y;

                    GL.BlitFramebuffer(
                        0, 0, width, height,
                        0, 0, width, height,
                        ClearBufferMask.DepthBufferBit,
                        BlitFramebufferFilter.Nearest
                    );

                    GL.BindFramebuffer(FramebufferTarget.Framebuffer, camera.OitFbo);

                    float[] clearZero = [0.0f, 0.0f, 0.0f, 0.0f];

                    GL.ClearBuffer(ClearBuffer.Color, 0, clearZero);
                    GL.ClearBuffer(ClearBuffer.Color, 1, clearZero);

                    GL.Enable(EnableCap.DepthTest);
                    GL.DepthFunc(DepthFunction.Less);
                    GL.DepthMask(false);

                    GL.Enable(EnableCap.Blend);
                    GL.BlendFunc(0, BlendingFactorSrc.One, BlendingFactorDest.One);
                    GL.BlendFunc(1, BlendingFactorSrc.One, BlendingFactorDest.One);


                    foreach (var s in SceneManager.CurrentScene.RenderingComponents.Where(s =>
                                 s is { IsOpaque: false, IsEnabled: true }))
                    {
                        if (SceneManager.SceneChanged)
                        {
                            SceneManager.SceneChanged = false;
                            return;
                        }

                        using (FrameProfiler.Scope(s.GetType().Name))
                        {
                            s.Render(deltaTime);
                        }
                    }

                    ppBuffer.Bind();

                    GL.DepthMask(true);
                    GL.Disable(EnableCap.DepthTest);

                    GL.Enable(EnableCap.Blend);
                    GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

                    OitShaderProgram.Use();
                    OitShaderProgram.SetValue("accumColorTex", 0);
                    OitShaderProgram.SetValue("accumWeightTex", 1);

                    GL.ActiveTexture(TextureUnit.Texture0);
                    GL.BindTexture(TextureTarget.Texture2D, camera.AccumColorTex);

                    GL.ActiveTexture(TextureUnit.Texture1);
                    GL.BindTexture(TextureTarget.Texture2D, camera.AccumWeightTex);

                    GL.BindVertexArray(FullscreenVao);
                    GL.DrawArrays(PrimitiveType.Triangles, 0, 3);

                    GL.Enable(EnableCap.DepthTest);

                    GL.BindTexture(TextureTarget.Texture2D, 0);
                    GL.ActiveTexture(TextureUnit.Texture0);
                    GL.BindTexture(TextureTarget.Texture2D, 0);

                    ppBuffer.Unbind();
                }

                using (FrameProfiler.Scope("post process"))
                {
                    ImGui.Begin($"Post Processing pipeline ({camera.Name})");
                    ImGui.InputText("Resource location", ref resLoc, 256);
                    ImGui.SameLine();
                    if (ImGui.Button("Add"))
                    {
                        if (ContentManager.Exists(resLoc))
                        {
                            camera.PostProcessLayers.Add(new(resLoc));
                            resLoc = "";
                            ToastManager.InsertNotification(new(ToastType.Success));
                        }
                        else
                        {
                            ToastManager.InsertNotification(new(ToastType.Error, "Shader doesnt exist"));
                        }
                    }


                    ImGui.Image(new ImTextureRef
                        {
                            TexID = new ImTextureID(ppBuffer.ColorTexture)
                        },
                        new Vector2(ppBuffer.Width, ppBuffer.Height) / 5, new(0, 1), new(1, 0));

                    var previousFb = ppBuffer;

                    PostProcessLayer r = null!;
                    foreach (var layer in camera.PostProcessLayers)
                    {
                        using (FrameProfiler.Scope(Path.GetFileName(layer.FragmentShader.AssetPath!)))
                        {
                            previousFb = layer.Draw(previousFb);
                        }

                        ImGui.Separator();
                        ImGui.Text(layer.FragmentShader.AssetPath);
                        ImGui.SameLine();
                        if (ImGui.Button("Remove ##" + layer.GetHashCode()))
                        {
                            r = layer;
                        }

                        ImGui.Image(new ImTextureRef { TexID = new ImTextureID(previousFb.ColorTexture) },
                            new Vector2(previousFb.Width, previousFb.Height) / 5, new(0, 1), new(1, 0));
                    }

                    if (r != null!)
                        camera.PostProcessLayers.Remove(r);

                    camera.RenderTexture =
                        StaticTexture.FromGlHandle((uint)previousFb.ColorTexture,
                            previousFb.Width, previousFb.Height);
                    ImGui.End();
                }


                using (FrameProfiler.Scope("hud"))
                {
                    Hud.Render();
                }
            }

            Camera._activeCamera = active;
        }
    }
}