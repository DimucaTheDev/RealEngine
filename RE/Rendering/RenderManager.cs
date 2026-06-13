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
using RE.Utils;
using Serilog;

namespace RE.Rendering
{
    public static class RenderManager
    {
        private static string resLoc = "";

        public static readonly List<PostProcessLayer> PostProcessLayers =
        [
            new("Assets/Shaders/Pass/Postprocess/ca.frag")
        ];

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

        public static void RenderAll(FrameEventArgs args)
        {
            if (!SceneEditor.Enabled && SceneManager.CurrentScene != null!)
            {
                var ppBuffer = Game.Instance.PrePostProcessFramebuffer;

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
                            s.Render(args);
                        }
                    }

                    GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, ppBuffer.Handle);
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
                            s.Render(args);
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
                    GL.BindTexture(TextureTarget.Texture2D, Game.Instance.AccumColorTex);

                    GL.ActiveTexture(TextureUnit.Texture1);
                    GL.BindTexture(TextureTarget.Texture2D, Game.Instance.AccumWeightTex);

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
                    ImGui.Begin("Post Processing pipeline");
                    ImGui.InputText("Resource location", ref resLoc, 256);
                    ImGui.SameLine();
                    if (ImGui.Button("Add"))
                    {
                        if (ContentManager.Exists(resLoc))
                        {
                            PostProcessLayers.Add(new(resLoc));
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
                        Game.Instance.ClientSize.ToVector2().ToSystemVector2() / 4, new(0, 1), new(1, 0));

                    var previousFb = ppBuffer;

                    PostProcessLayer r = null!;
                    foreach (var layer in PostProcessLayers)
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
                            Game.Instance.ClientSize.ToVector2().ToSystemVector2() / 4, new(0, 1), new(1, 0));
                    }

                    if (r != null!)
                        PostProcessLayers.Remove(r);

                    PostProcessLayer.Default.Draw(previousFb);
                    ImGui.End();
                }


                using (FrameProfiler.Scope("hud"))
                {
                    Hud.Render();
                }

                //LineRenderer.Main.Render(args);
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
    }
}