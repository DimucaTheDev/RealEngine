using System.Diagnostics;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Hexa.NET.ImGui;
using OpenTK.Graphics.OpenGL;
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
using RE.Rendering;
using RE.Rendering.Texturing;
using RE.Utils;
using Serilog;
using static Hexa.NET.ImGui.ImGui;
using Image = System.Drawing.Image;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
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
        public static bool PreviewLight, PreviewSkybox, ShowAxis = true, ShowGrid = true, PreviewParticles, ShowHud;
        public static GameObject? SelectedObject;
        public static bool ShowExitConfirmationModal;
        public static bool SimulationRunning;

        private static readonly ImFontPtr _bigFont;
        private static readonly Texture LogoImage;

        private static bool _isDockspaceOpen;

        private Scene _scene = null!;
        private Dictionary<string, List<Type>> _componentDict = new();
        private Node _rootNode = new();
        private string _oldTitle;
        private string _preSimulationSceneJson;
        
        private readonly List<Type> _customPopups = new();
        private readonly HierarchyPanel _hierarchyPanel = new();
        private readonly HudHierarchyPanel _hudHierarchyPanel = new();
        private readonly InspectorPanel _inspectorPanel = new();
        private readonly AssetBrowserPanel _assetBrowserPanel = new();
        private readonly ViewportPanel _viewportPanel = new();
        private readonly ConsoleWindow _consoleWindow = new() { Id = "Editor" };

        static SceneEditor()
        {
            var iconPath = ("Assets/AppIcon.ico");

            if (ContentManager.Exists(iconPath))
            {
                var maxSize = new Size(0, 0);
                var mem = ContentManager.Open(iconPath);

                using var tmp = new Icon(mem, new Size(512, 512));
                if (tmp.Width > maxSize.Width && tmp.Height > maxSize.Height)
                    maxSize = new Size(tmp.Width, tmp.Height);

                mem.Position = 0;

                using var bestIcon = new Icon(mem, maxSize);
                using var bmp = bestIcon.ToBitmap();

                mem.Position = 0;

                var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
                    ImageLockMode.ReadOnly,
                    PixelFormat.Format32bppArgb);

                var bytesPerPixel = Image.GetPixelFormatSize(bmp.PixelFormat) / 8;
                var stride = bmp.Width * bytesPerPixel;
                var totalBytes = stride * bmp.Height;
                var pixelData = new byte[totalBytes];
                Marshal.Copy(data.Scan0, pixelData, 0, totalBytes);
                bmp.UnlockBits(data);

                LogoImage = new StaticTexture(pixelData, bmp.Width, bmp.Height);
            }
            else
            {
                LogoImage = StaticTexture.CreateMissingTexture(6);
            } 
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

            _scene = SceneManager.CurrentScene;
            IsVisible = true;

            foreach (var type in Assembly.GetExecutingAssembly().GetTypes()
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
        }

        public void Disable()
        {
            if (!Enabled)
                return;

            Enabled = false;
            IsVisible = false;

            Game.Instance.Title = _oldTitle;

            var reloaded = SceneManager.Reload(_scene);
            _scene.Dispose();
            SceneManager.LoadScene(reloaded);
        }

        public override void Render(FrameEventArgs args)
        {
            FrameProfiler.Begin("editor");
            FrameProfiler.Begin("update");
            foreach (var obj in _scene.GameObjects)
            {
                if (!obj.Components.Any())
                    continue;

                FrameProfiler.Begin(obj.Name ?? $"<{obj.Id}>");
                foreach (var com in obj.Components)
                {
                    // ReSharper disable once SuspiciousTypeConversion.Global
                    if (com is IEditorUpdate u)
                    {
                        FrameProfiler.Begin(com.GetType().Name);
                        u.EditorUpdate(args);
                        FrameProfiler.End();
                    }
                }

                FrameProfiler.End();
            }

            FrameProfiler.End();

            FrameProfiler.Begin("render");
            {
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, Game.Instance.SceneFboId);

                GL.Enable(EnableCap.DepthTest);
                GL.DepthMask(true);
                GL.Disable(EnableCap.Blend);

                foreach (var s in RenderManager.RenderingComponents.Where(s =>
                             s is { IsOpaque: true, IsEnabled: true }))
                {
                    if (SceneManager.SceneChanged)
                    {
                        SceneManager.SceneChanged = false;
                        return;
                    }

                    FrameProfiler.Begin(s.GetType().Name);
                    if (s is IEditorRender r)
                    {
                        r.EditorRender(args);
                    }

                    FrameProfiler.End();
                }

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

                float[] clearZero = { 0.0f, 0.0f, 0.0f, 0.0f };

                GL.ClearBuffer(ClearBuffer.Color, 0, clearZero);
                GL.ClearBuffer(ClearBuffer.Color, 1, clearZero);

                GL.Enable(EnableCap.DepthTest);
                GL.DepthFunc(DepthFunction.Less);
                GL.DepthMask(false);

                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(0, BlendingFactorSrc.One, BlendingFactorDest.One);
                GL.BlendFunc(1, BlendingFactorSrc.One, BlendingFactorDest.One);


                foreach (var s in RenderManager.RenderingComponents.Where(s =>
                             s is { IsOpaque: false, IsEnabled: true } or RigidBodyComponent))
                {
                    if (SceneManager.SceneChanged)
                    {
                        SceneManager.SceneChanged = false;
                        return;
                    }

                    FrameProfiler.Begin(s.GetType().Name);
                    s.Render(args);
                    if (s is IEditorRender r)
                    {
                        r.EditorRender(args);
                    }

                    FrameProfiler.End();
                }


                GL.BindFramebuffer(FramebufferTarget.Framebuffer, Game.Instance.SceneFboId);

                GL.DepthMask(true);
                GL.Disable(EnableCap.DepthTest);

                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

                RenderManager.OitShaderProgram.Use();

                GL.Uniform1(RenderManager.OitShaderProgram.GetLocation("accumColorTex"), 0);
                GL.Uniform1(RenderManager.OitShaderProgram.GetLocation("accumWeightTex"), 1);

                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, Game.Instance.AccumColorTex);

                GL.ActiveTexture(TextureUnit.Texture1);
                GL.BindTexture(TextureTarget.Texture2D, Game.Instance.AccumWeightTex);

                GL.BindVertexArray(RenderManager.FullscreenVao);
                GL.DrawArrays(PrimitiveType.Triangles, 0, 3);

                GL.Enable(EnableCap.DepthTest);

                GL.BindTexture(TextureTarget.Texture2D, 0);
                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, 0);
            }
            FrameProfiler.End();

            if (ShowHud)
            {
                FrameProfiler.Begin("hud");
                Hud.Render();
                FrameProfiler.End();
            }

            if (SceneManager.CurrentScene == null!)
                return;

            SetupDockSpace();

            _hierarchyPanel.Draw();
            _hudHierarchyPanel.Draw();
            _inspectorPanel.Draw();
            _assetBrowserPanel.Draw();
            _viewportPanel.Draw();
            _consoleWindow.Render(args);

            ShowExitModalWindow();
            FrameProfiler.End();
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
                        Game.Instance.Close();
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

        private bool _isFirstTime = true;

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
                        SceneManager.SaveSceneToFile(_scene, "assets/maps/demo");
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
                uint nodeViewport;
                ImGuiP.DockBuilderSplitNode(nodeTop, ImGuiDir.Left, 0.20f, &nodeHierarchy, &nodeViewport);

                uint nodeAssetBrowser;
                uint nodeConsole;
                ImGuiP.DockBuilderSplitNode(nodeBottom, ImGuiDir.Left, 0.59f, &nodeAssetBrowser, &nodeConsole);

                ImGuiP.DockBuilderDockWindow("Scene Hierarchy", nodeHierarchy);
                ImGuiP.DockBuilderDockWindow("UI Hierarchy", nodeHierarchy);
                ImGuiP.DockBuilderDockWindow("Viewport", nodeViewport);
                ImGuiP.DockBuilderDockWindow("Inspector", nodeInspector);
                ImGuiP.DockBuilderDockWindow("Asset browser", nodeAssetBrowser);
                ImGuiP.DockBuilderDockWindow("Console ##Editor", nodeConsole);

                ImGuiP.DockBuilderFinish(dockspaceId);
            }

            End();
        }
    }
}