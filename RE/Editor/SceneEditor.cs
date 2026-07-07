using System.Diagnostics;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Hexa.NET.ImGui;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core;
using RE.Core.Assets;
using RE.Core.Audio;
using RE.Core.Input;
using RE.Core.PluginSystem;
using RE.Core.Scripting;
using RE.Core.Scripting.Attributes;
using RE.Core.Ui;
using RE.Core.Ui.Debug;
using RE.Core.World;
using RE.Core.World.Components.Physics;
using RE.Editor.Notification;
using RE.Editor.Panels;
using RE.Editor.Panels.Viewport;
using RE.Editor.Utils;
using RE.Rendering;
using RE.Rendering.Texturing;
using RE.Utils;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using static Hexa.NET.ImGui.ImGui;
using Image = SixLabors.ImageSharp.Image;
using Vector2 = System.Numerics.Vector2;

namespace RE.Editor
{
    public partial class SceneEditor : Renderable
    {
        private class Node
        {
            public string Name = string.Empty;
            public readonly Dictionary<string, Node> Children = new();
            public readonly List<Type> Types = new();
        }

        public SceneEditor() => this.StartRender();

        public override bool IsVisible { get; set; }

        public static SceneEditor Instance;
        public static bool Enabled;

        public static bool PreviewLight = true,
            PreviewSkybox,
            ShowAxis = true,
            ShowGrid = true,
            PreviewParticles,
            ShowHud;

        public static GameObject? SelectedObject;
        public static bool ShowExitConfirmationModal;
        public static bool SimulationRunning;

        internal static OutlineFramebuffer OutlineFramebuffer;
        internal static bool GamePanelActive;

        private static readonly ImFontPtr _bigFont;
        private static readonly Texture LogoImage;
        private static readonly ShaderProgram OutlineShaderProgram = new();

        private static bool _isFirstTime = true;
        private static bool _isDockspaceOpen;

        private Scene Scene => SceneManager.CurrentScene;

        private Dictionary<string, List<Type>> _componentDict = new();
        private Node _rootNode = new();
        private string _oldTitle;

        private readonly List<Type> _customPopups = new();
        private readonly HierarchyPanel _hierarchyPanel = new();
        private readonly HudHierarchyPanel _hudHierarchyPanel = new();
        private readonly InspectorPanel _inspectorPanel = new();
        private readonly AssetBrowserPanel _assetBrowserPanel = new();
        private readonly ViewportPanel _viewportPanel = new();
        private readonly GamePanel _gamePanel = new();
        private readonly ConsoleWindow _consoleWindow = new() { Id = "Editor" };

        static SceneEditor()
        {
            var iconPath = ("Assets/AppIcon.ico");

            if (ContentManager.Exists(iconPath))
            {
                using var mem = ContentManager.Open(iconPath);
                using var image = Image.Load<Bgra32>(mem);

                ImageFrame<Bgra32> bestFrame = image.Frames[0];
                int maxArea = 0;

                foreach (var frame in image.Frames)
                {
                    if (frame.Width <= 512 && frame.Height <= 512)
                    {
                        int area = frame.Width * frame.Height;
                        if (area > maxArea)
                        {
                            maxArea = area;
                            bestFrame = frame;
                        }
                    }
                }

                int width = bestFrame.Width;
                int height = bestFrame.Height;
                byte[] pixelData = new byte[width * height * 4];

                bestFrame.CopyPixelDataTo(pixelData);

                LogoImage = new StaticTexture(new ImageData(pixelData, width, height));
            }
            else
            {
                LogoImage = StaticTexture.CreateMissingTexture(6);
            }

            OutlineShaderProgram.AttachShader("Assets/Shaders/Pass/Editor/outline.frag");
            OutlineShaderProgram.AttachShader("Assets/Shaders/Pass/Editor/outline.vert");
            OutlineFramebuffer = new();
        }

        public void Enable()
        {
#if DISABLE_SCENE_EDITOR
            Log.Error("Scene Editor is disabled.");
            return;
#endif

            if (SceneManager.CurrentScene == null!)
            {
                Log.Error("Editor can not be opened if no scene is loaded.");
                return;
            }

            Enabled = true;
            SoundManager.StopAll();
            ToastManager.RemoveAllNotifications();
            Mouse.CursorState = CursorState.Normal;


            _oldTitle = Game.Instance.Title;
            //Game.Instance.Title = $"{Game.ProductName} Scene Editor {Game.Version} [{Game.CommitHash[..7]}, {Game.BuildDate:g}] | Scene \"{SceneManager.CurrentScene.Name ?? "<Unnamed>"}\"";

            Log.Information("Starting Scene Editor for \"{SceneName}\"...", SceneManager.CurrentScene.Name);

            IsVisible = true;

            _customPopups.Clear();
            foreach (var type in Assembly
                         .GetExecutingAssembly()
                         .GetTypes()
                         .Where(t => typeof(IEditorPopup).IsAssignableFrom(t)))
            {
                _customPopups.Add(type);
            }

            _componentDict = new[] { Assembly.GetExecutingAssembly() }
                .Concat(PluginManager.LoadedPlugins.Select(s => s.PluginInformation.Assembly))
                .SelectMany(assembly => assembly.GetTypes())
                .Distinct()
                .Where(t => !t.IsAbstract && typeof(Component).IsAssignableFrom(t))
                .GroupBy(type =>
                {
                    var attr = type.GetCustomAttribute<ComponentInfoAttribute>();
                    return attr?.Group ?? "Other";
                })
                .ToDictionary(g => g.Key, g => g.ToList());
            _rootNode = new Node();
            foreach (var entry in _componentDict)
            {
                string[] pathSegments = entry.Key.Split('/');
                Node currentNode = _rootNode;
                foreach (var segment in pathSegments)
                {
                    if (!currentNode.Children.TryGetValue(segment, out Node nextNode))
                    {
                        nextNode = new Node { Name = segment };
                        currentNode.Children.Add(segment, nextNode);
                    }

                    currentNode = nextNode;
                }

                currentNode.Types.AddRange(entry.Value);
            }

            SceneManager.CurrentScene =
                SceneSerializer.DeserializeScene(SceneSerializer.SerializeScene(SceneManager.CurrentScene));
        }

        public void Disable()
        {
            if (!Enabled)
                return;

            Enabled = false;
            IsVisible = false;

            Game.Instance.Title = _oldTitle;

            SceneManager.Reload(Scene);
        }

        public override void Render(double delta)
        {
            Camera._activeCamera = Camera.ViewportCamera;

            using (FrameProfiler.Scope("editor"))
            {
                using (FrameProfiler.Scope("update"))
                {
                    //if(Keyboard.CanCaptureInput)

                    foreach (var obj in Scene.GameObjects)
                    {
                        if (!obj.Components.Any())
                            continue;

                        using (FrameProfiler.Scope(obj.Name ?? $"<{obj.Id}>"))
                        {
                            foreach (var com in obj.Components)
                            {
                                // ReSharper disable once SuspiciousTypeConversion.Global
                                if (com is IEditorUpdate u)
                                {
                                    using (FrameProfiler.Scope(com.GetType().Name))
                                    {
                                        u.EditorUpdate(delta);
                                    }
                                }
                            }
                        }
                    }
                }

                using (FrameProfiler.Scope("render"))
                {
                    Camera.ViewportCamera.SceneFramebuffer.Bind();
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
                            return;
                        }

                        using (FrameProfiler.Scope(s.GetType().Name))
                        {
                            if (s is IEditorRender r)
                            {
                                r.EditorRender(delta);
                            }
                        }
                    }

                    GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, Camera.ViewportCamera.OitFbo);

                    int width = (int)ViewportPanel.ViewportSize.X;
                    int height = (int)ViewportPanel.ViewportSize.Y;

                    GL.BlitFramebuffer(
                        0, 0, width, height,
                        0, 0, width, height,
                        ClearBufferMask.DepthBufferBit,
                        BlitFramebufferFilter.Nearest
                    );

                    GL.BindFramebuffer(FramebufferTarget.Framebuffer, Camera.ViewportCamera.OitFbo);

                    float[] clearZero = { 0.0f, 0.0f, 0.0f, 0.0f };

                    GL.ClearBuffer(ClearBuffer.Color, 0, clearZero);
                    GL.ClearBuffer(ClearBuffer.Color, 1, clearZero);

                    GL.Enable(EnableCap.DepthTest);
                    GL.DepthFunc(DepthFunction.Less);
                    GL.DepthMask(false);

                    GL.Enable(EnableCap.Blend);
                    GL.BlendFunc(0, BlendingFactorSrc.One, BlendingFactorDest.One);
                    GL.BlendFunc(1, BlendingFactorSrc.One, BlendingFactorDest.One);


                    foreach (var s in Scene.RenderingComponents.Where(s =>
                                 s is { IsOpaque: false, IsEnabled: true } or RigidBodyComponent))
                    {
                        if (SceneManager.SceneChanged)
                        {
                            SceneManager.SceneChanged = false;
                            return;
                        }

                        using (FrameProfiler.Scope(s.GetType().Name))
                        {
                            s.Render(delta);
                            if (s is IEditorRender r)
                            {
                                r.EditorRender(delta);
                            }
                        }
                    }


                    Camera.ViewportCamera.SceneFramebuffer.Bind();

                    GL.DepthMask(true);
                    GL.Disable(EnableCap.DepthTest);

                    GL.Enable(EnableCap.Blend);
                    GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

                    RenderManager.OitShaderProgram.Use();

                    GL.Uniform1(RenderManager.OitShaderProgram.GetLocation("accumColorTex"), 0);
                    GL.Uniform1(RenderManager.OitShaderProgram.GetLocation("accumWeightTex"), 1);

                    GL.ActiveTexture(TextureUnit.Texture0);
                    GL.BindTexture(TextureTarget.Texture2D, Camera.ViewportCamera.AccumColorTex);

                    GL.ActiveTexture(TextureUnit.Texture1);
                    GL.BindTexture(TextureTarget.Texture2D, Camera.ViewportCamera.AccumWeightTex);

                    GL.BindVertexArray(RenderManager.FullscreenVao);
                    GL.DrawArrays(PrimitiveType.Triangles, 0, 3);

                    GL.Enable(EnableCap.DepthTest);

                    GL.BindTexture(TextureTarget.Texture2D, 0);
                    GL.ActiveTexture(TextureUnit.Texture0);
                    GL.BindTexture(TextureTarget.Texture2D, 0);

                    LineRenderer.Main.Render(delta); // ?????
                }


                if (SelectedObject != null)
                {
                    using (FrameProfiler.Scope("outline"))
                    {
                        OutlineFramebuffer.Bind();

                        GL.Enable(EnableCap.StencilTest);
                        GL.StencilMask(0xFF);

                        GL.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

                        GL.StencilFunc(StencilFunction.Always, 1, 0xFF);
                        GL.StencilOp(StencilOp.Replace, StencilOp.Replace, StencilOp.Replace);

                        GL.ColorMask(false, false, false, false);

                        foreach (var component in SelectedObject.Components.Where(s => s.IsEnabled)
                                     .OfType<IEditorRender>())
                        {
                            component.EditorRender(delta);
                        }

                        GL.ColorMask(true, true, true, true);
                        GL.Disable(EnableCap.StencilTest);

                        Camera.ViewportCamera.SceneFramebuffer.Bind();

                        OutlineShaderProgram.Use();
                        OutlineShaderProgram.SetValue("u_StencilTexture", 0);

                        GL.Enable(EnableCap.Blend);
                        GL.Disable(EnableCap.DepthTest);
                        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

                        GL.ActiveTexture(TextureUnit.Texture0);
                        GL.BindTexture(TextureTarget.Texture2D, OutlineFramebuffer.DepthStencilTexture);

                        GL.BindVertexArray(RenderManager.FullscreenVao);
                        GL.DrawArrays(PrimitiveType.Triangles, 0, 3);

                        GL.Enable(EnableCap.DepthTest);
                        GL.Disable(EnableCap.Blend);
                    }
                }

                if (ShowHud)
                {
                    using (FrameProfiler.Scope("hud"))
                    {
                        Hud.Render();
                    }
                }

                LineRenderer.Main.Render(delta);

                if (SceneManager.CurrentScene == null!)
                    return;

                SetupDockSpace();

                _hierarchyPanel.Draw();
                _hudHierarchyPanel.Draw();
                _inspectorPanel.Draw();
                _assetBrowserPanel.Draw();
                _viewportPanel.Draw();
                _gamePanel.Draw();
                _consoleWindow.Render(delta);

                ShowExitModalWindow();
            }
        }

        private double _exitButtonWait;

        private void ShowExitModalWindow()
        {
            if (ShowExitConfirmationModal)
            {
                OpenPopup("Exit Confirmation");
                if (BeginPopupModal("Exit Confirmation", ref ShowExitConfirmationModal,
                        ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings))
                {
                    Text("Are you sure you want to exit the Scene Editor?\nUnsaved changes will be lost.");
                    Separator();

                    BeginDisabled((_exitButtonWait += Time.DeltaTime) <= 3);

                    var label = _exitButtonWait > 3 ? "Yes" : $"Yes ({(3 - (int)_exitButtonWait)}s)";
                    if (Button(label))
                    {
                        _exitButtonWait = 0;
                        Disable();
                        CloseCurrentPopup();
                        //Game.Instance.Close();
                    }

                    EndDisabled();

                    SameLine(90);
                    if (Button("No"))
                    {
                        CloseCurrentPopup();
                        _exitButtonWait = 0;
                        ShowExitConfirmationModal = false;
                        unsafe
                        {
                            WinApi.StopFlashing((IntPtr)Game.Instance.WindowPtr);
                        }
                    }

                    EndPopup();
                }
            }
        }

        private unsafe void SetupDockSpace()
        {
            var io = GetIO();
            var mainViewport = GetMainViewport();

            SetNextWindowViewport(mainViewport.ID);
            SetNextWindowPos(mainViewport.Pos);
            SetNextWindowSize(mainViewport.Size);

            ImGuiWindowFlags windowFlags =
                ImGuiWindowFlags.NoTitleBar |
                ImGuiWindowFlags.NoResize |
                ImGuiWindowFlags.NoMove |
                ImGuiWindowFlags.NoBringToFrontOnFocus |
                ImGuiWindowFlags.NoBackground |
                ImGuiWindowFlags.MenuBar;

            PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
            PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);

            Begin("MainDockspaceWindow", ref _isDockspaceOpen, windowFlags);

            if (BeginMenuBar())
            {
                if (BeginMenu("Editor"))
                {
                    if (MenuItem("Create new scene"))
                    {
                    }

                    if (MenuItem("Open scene"))
                    {
                    }

                    if (MenuItem("Save scene"))
                    {
                        SceneManager.SaveSceneToFile(Scene, "assets/maps/demo");
                    }

                    if (MenuItem("Save scene as"))
                    {
                    }

                    if (BeginMenu("Preferences"))
                    {
                        Selectable("Render Skybox", ref PreviewSkybox);
                        Selectable("Preview Light", ref PreviewLight);
                        EndMenu();
                    }

                    if (MenuItem("Settings"))
                    {
                    }

                    Separator();
                    if (MenuItem("Exit"))
                    {
                        ShowExitConfirmationModal = true;
                        WinApi.StartFlashing((IntPtr)Game.Instance.WindowPtr);
                    }

                    EndMenu();
                }

                if (BeginMenu("Objects"))
                {
                    if (MenuItem("New Blank"))
                    {
                        var newObject = new GameObject();
                        SceneManager.CurrentScene.GameObjects.Add(newObject);
                    }

                    if (MenuItem("New Blank 2"))
                    {
                        var newObject = new GameObject();
                        SceneManager.CurrentScene.GameObjects.Add(newObject);

                        var newObject2 = new GameObject();
                        SceneManager.CurrentScene.GameObjects.Add(newObject2);
                        newObject.Parent = newObject2;
                    }

                    EndMenu();
                }

                if (BeginMenu("Tools"))
                {
                    if (MenuItem("Console"))
                    {
                        var consoleWindow = new ConsoleWindow { Id = $"NewConsoleInstance{Random.Shared.Next(10000)}" };
                        consoleWindow.StartRender();
                        consoleWindow.IsVisible = true;
                    }

                    if (MenuItem("Skybox Editor"))
                    {
                    }

                    if (MenuItem("Particle Editor"))
                    {
                    }

                    if (MenuItem("Model Browser"))
                    {
                    }

                    if (MenuItem("Model Converter"))
                    {
                    }

                    if (MenuItem("Var Editor"))
                    {
                    }

                    EndMenu();
                }

                if (BeginMenu("Help"))
                {
                    if (MenuItem("Open Docs"))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "https://dimucathedev.github.io/RealEngine/docs/editor/about.html",
                            UseShellExecute = true
                        });
                    }

                    Separator();

                    if (MenuItem("About"))
                    {
                    }

                    EndMenu();
                }

                EndMenuBar();
            }


            PopStyleVar(2);

            uint dockspaceId = GetID("EditorDockSpace");

            DockSpace(dockspaceId, new Vector2(0, 0), ImGuiDockNodeFlags.PassthruCentralNode);

            if (_isFirstTime)
            {
                _isFirstTime = false;

                ImGuiP.DockBuilderRemoveNode(dockspaceId);
                ImGuiP.DockBuilderAddNode(dockspaceId, ImGuiDockNodeFlags.None);
                ImGuiP.DockBuilderSetNodeSize(dockspaceId, new Vector2(1904, 974));

                uint nodeLeft;
                uint nodeInspector;
                ImGuiP.DockBuilderSplitNode(dockspaceId, ImGuiDir.Left, 0.79f, &nodeLeft, &nodeInspector);

                uint nodeTop;
                uint nodeBottom;
                ImGuiP.DockBuilderSplitNode(nodeLeft, ImGuiDir.Up, 0.68f, &nodeTop, &nodeBottom);

                uint nodeHierarchy;
                uint nodeViewportParent;
                ImGuiP.DockBuilderSplitNode(nodeTop, ImGuiDir.Left, 0.20f, &nodeHierarchy, &nodeViewportParent);

                uint nodeViewport;
                uint nodeGamePanel;
                ImGuiP.DockBuilderSplitNode(nodeViewportParent, ImGuiDir.Right, 0.50f, &nodeGamePanel, &nodeViewport);

                uint nodeAssetBrowser;
                uint nodeConsole;
                ImGuiP.DockBuilderSplitNode(nodeBottom, ImGuiDir.Left, 0.59f, &nodeAssetBrowser, &nodeConsole);

                ImGuiP.DockBuilderDockWindow("Scene Hierarchy", nodeHierarchy);
                ImGuiP.DockBuilderDockWindow("UI Hierarchy", nodeHierarchy);
                ImGuiP.DockBuilderDockWindow("Viewport", nodeViewport);
                ImGuiP.DockBuilderDockWindow("###game_panel", nodeGamePanel);
                ImGuiP.DockBuilderDockWindow("Inspector", nodeInspector);
                ImGuiP.DockBuilderDockWindow("Asset browser", nodeAssetBrowser);
                ImGuiP.DockBuilderDockWindow("Console ##Editor", nodeConsole);

                ImGuiP.DockBuilderFinish(dockspaceId);
            }

            End();
        }
    }
}