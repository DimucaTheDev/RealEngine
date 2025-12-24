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
using RE.Core.PluginSystem;
using RE.Core.Scripting;
using RE.Core.World;
using RE.Debug.Overlay.Editor.Panels;
using RE.Rendering;
using RE.Rendering.Renderables;
using RE.Utils;
using Serilog;
using static Hexa.NET.ImGui.ImGui;
using Image = System.Drawing.Image;
using PixelFormat = OpenTK.Graphics.OpenGL.PixelFormat;
using Vector2 = System.Numerics.Vector2;

namespace RE.Debug.Overlay.Editor
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

        public override RenderLayer RenderLayer => RenderLayer.ImGui;
        public override bool IsVisible { get; set; } = false;

        public static SceneEditor Instance;
        public static bool Enabled = false;
        public static bool PreviewLight, PreviewSkybox, ShowAxis = true, ShowGrid = true;
        public static GameObject? SelectedObject;
        public static bool ShowExitConfirmationModal = false;

        private static readonly ImFontPtr _bigFont;
        private static readonly ModelRenderer SelectedObjectOutline = new() { Outline = true };
        private static readonly Texture LogoImage;

        private static bool _isDockspaceOpen;

        private Scene _scene = null!;
        private GameObject? _selectedObject;
        private Dictionary<string, List<Type>> _componentDict = new();
        private Node _rootNode = new();
        private readonly List<Type> _customPopups = new();
        private readonly HierarchyPanel _hierarchyPanel = new();
        private readonly InspectorPanel _inspectorPanel = new();
        private readonly AssetBrowserPanel _assetBrowserPanel = new();
        private readonly ViewportPanel _viewportPanel = new();
        private readonly ConsoleWindow _consoleWindow = new() { Id = "Editor" };

        static SceneEditor()
        {
            Variables.VariableChanged += (s, e) =>
            {
                if (s == "selectColor")
                {
                    var propertyInfo = typeof(Color4)
                        .GetProperty(e?.ToString() ?? "red",
                            BindingFlags.IgnoreCase | BindingFlags.Static | BindingFlags.Public)!;
                    if (propertyInfo == null!)
                    {
                        var props = typeof(Color4).GetProperties(BindingFlags.Static | BindingFlags.Public)
                            .Where(prop => prop.PropertyType == typeof(Color4));
                        Log.Error("incorrect color '{Color}'. Possible values: {PossibleValues}", e, string.Join("; ", props.Select(s => s.Name)));
                        return;
                    }

                    var color = (Color4)propertyInfo.GetValue(null)!;
                    OpenTK.Mathematics.Vector4 outlineColor = new(color.R, color.G, color.B, color.A);
                    SelectedObjectOutline.OutlineColor = outlineColor;
                }
            };

            var iconPath = ($"Assets/RealEngine{(Random.Shared.Next(100) > 50 ? "2" : "")}.ico");

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
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                var bytesPerPixel = Image.GetPixelFormatSize(bmp.PixelFormat) / 8;
                var stride = bmp.Width * bytesPerPixel;
                var totalBytes = stride * bmp.Height;
                var pixelData = new byte[totalBytes];
                Marshal.Copy(data.Scan0, pixelData, 0, totalBytes);
                bmp.UnlockBits(data);

                LogoImage = new Texture(pixelData, bmp.Width, bmp.Height);
            }
            else
            {
                LogoImage = Texture.CreateMissingTexture(6);
            }
            GL.BindTexture(TextureTarget.Texture2D, 0);

            unsafe
            {
                //todo: cyrillic font for imgui
            }
        }

        public void Enable()
        {
#if PRODUCTION || PROD // + RELEASE?
            Log.Error("Scene Editor is disabled in production builds.");
            return;
#endif

            if (SceneManager.CurrentScene == null!)
            {
                Log.Error("Editor can not be opened if no scene is loaded.");
                return;
            }
            Enabled = true;

            Game.Instance.CursorState = CursorState.Normal;

            Log.Information("Starting Scene Editor for \"{SceneName}\"...", SceneManager.CurrentScene.Name);

            _scene = SceneManager.CurrentScene; 
            IsVisible = true;

            foreach (var type in Assembly.GetExecutingAssembly().GetTypes().Where(t => typeof(IEditorPopup).IsAssignableFrom(t)))
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
            SelectedObjectOutline?.StopRender();

            var reloaded = SceneManager.Reload(_scene);
            _scene.Dispose();
            SceneManager.LoadScene(reloaded);
        }

        public override void Render(FrameEventArgs args)
        {
            foreach (var obj in _scene.GameObjects)
            {
                foreach (var com in obj.Components)
                {
                    if (com is IDebugRenderer s)
                    {
                        s.DebugRender(args);
                    }
                }
            }

            if (SceneManager.CurrentScene == null!)
                return;

            SetupDockSpace();

            _hierarchyPanel.Draw();
            _inspectorPanel.Draw();
            _assetBrowserPanel.Draw();
            _viewportPanel.Draw();
            _consoleWindow.Render(args);

            ShowExitModalWindow();
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

                    BeginDisabled((_exitButtonWait += Time.DeltaTime) <= 4);

                    var label = _exitButtonWait > 4 ? "Yes" : $"Yes ({(4 - (int)_exitButtonWait)}s)";
                    if (Button(label))
                    {
                        _exitButtonWait = 0;
                        Disable();
                        CloseCurrentPopup();
                        Environment.Exit(0);
                    }
                    EndDisabled();

                    SameLine(90);
                    if (Button("No"))
                    {
                        CloseCurrentPopup();
                        _exitButtonWait = 0;
                        ShowExitConfirmationModal = false;
                    }

                    EndPopup();
                }
            }
        }

        private void SetupDockSpace()
        {
            ImGuiIOPtr io = GetIO();

            SetNextWindowPos(new Vector2(0, 0));
            SetNextWindowSize(io.DisplaySize);

            ImGuiWindowFlags windowFlags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize |
                                           ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoBringToFrontOnFocus |
                                           ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.MenuBar;

            PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
            PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);

            ImGui.Begin("MainDockspaceWindow", ref _isDockspaceOpen, windowFlags);

            if (BeginMenuBar())
            {
                if (BeginMenu("Editor"))
                {
                    if (MenuItem("Create new scene"))
                    { }
                    if (MenuItem("Open scene"))
                    { }
                    if (MenuItem("Save scene"))
                    { }
                    if (MenuItem("Save scene as"))
                    { }
                    if (BeginMenu("Preferences"))
                    {
                        Selectable("Render Skybox", ref SceneEditor.PreviewSkybox);
                        Selectable("Preview Light", ref SceneEditor.PreviewLight);
                        EndMenu();
                    }
                    if (MenuItem("Settings"))
                    { }
                    Separator();
                    if (MenuItem("Exit"))
                    {
                        // renderQuit = true;
                    }
                    EndMenu();
                }

                if (BeginMenu("Objects"))
                {
                    if (MenuItem("New Blank"))
                    {
                        var newObject = new GameObject();
                        SceneManager.CurrentScene.GameObjects.Add(newObject);
                        //Main.SelectObject(newObject);
                    }
                    if (MenuItem("New Blank 2"))
                    {
                        var newObject = new GameObject();
                        SceneManager.CurrentScene.GameObjects.Add(newObject);

                        var newObject2 = new GameObject();
                        SceneManager.CurrentScene.GameObjects.Add(newObject2);
                        newObject.Parent = newObject2;

                        //Main.SelectObject(newObject);
                    }

                    EndMenu();
                }

                if (BeginMenu("Tools"))
                {
                    if (MenuItem("Skybox Editor"))
                    { }
                    if (MenuItem("Particle Editor"))
                    { }
                    if (MenuItem("Model Browser"))
                    { }
                    if (MenuItem("Model Converter"))
                    { }
                    if (MenuItem("Var Editor"))
                    { }
                    EndMenu();

                }
                if (BeginMenu("Help"))
                {
                    if (MenuItem("Open Docs"))
                    {
                        Process.Start("explorer", "https://dimucathedev.github.io/RealEngine/docs/editor/about.html");
                    }

                    Separator();

                    if (MenuItem("About"))
                    {
                        // renderAbout = true;
                    }

                    EndMenu();
                }
                EndMenuBar();
            }


            PopStyleVar(2);

            uint dockspaceId = GetID("EditorDockSpace");

            DockSpace(dockspaceId, new Vector2(0, 0), ImGuiDockNodeFlags.PassthruCentralNode);

            End();
        }

        #region Code bellow never gets called. legacy :)
        private PropertyInfo _p = null!;
        private string _searchComponent = null!;
        void DrawButton(Type type)
        {
            bool SatisfiesCondition(Type c, out List<Type> missing)
            {
                missing = new();
                var reqAtts = c.GetCustomAttributes<RequiresComponentAttribute>();
                if (_selectedObject!.Components.Any(s => s.GetType() == c))
                    return false;
                foreach (var att in reqAtts)
                {
                    if (_selectedObject!.Components.All(comp => comp.GetType() != att.RequiredComponent))
                        missing.Add(att.RequiredComponent);
                }
                return missing.Count == 0;
            }

            bool alreadyContains = _selectedObject!.Components.Any(s => s.GetType() == type);
            bool disabled = !SatisfiesCondition(type, out var missingComponents);

            BeginDisabled(disabled);
            if (Button(AddSpacesToCamelCase(type.Name.Replace("Component", ""))))
            {
                var c = Activator.CreateInstance(type);
                _selectedObject!.Components.Add((Component)c!);
                CloseCurrentPopup();
            }
            EndDisabled();

            if (IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                if (BeginTooltip())
                {
                    if (disabled)
                    {
                        if (alreadyContains)
                        {
                            Text($"This object already contains {type.Name}.");
                            Separator();
                        }
                        else if (missingComponents.Count > 0)
                        {
                            Text("Cannot add this component because the following required components are missing:");
                            foreach (var req in missingComponents)
                                Text($"- {AddSpacesToCamelCase(req.Name.Replace("Component", ""))} ({req.FullName} in {Path.GetFileName(req.Assembly.Location)})");
                            Separator();
                            Text("Please add the required components first.");
                            NewLine();
                        }
                    }
                    var a = type.GetCustomAttribute<ComponentInfoAttribute>();
                    if (a is { Description: not null })
                    {
                        Text($"{a.Description}");
                        Separator();
                    }
                    Text($"Full Name: {type.FullName}");
                    Text($"Assembly:  {type.Assembly.FullName}");
                    EndTooltip();
                }
            }
        }
        void RenderGroupRecursive(string[] path, int depth, List<Type> types)
        {
            if (depth >= path.Length)
            {
                foreach (var type in types.OrderBy(t => t.Name))
                {
                    if (Button(AddSpacesToCamelCase(type.Name.Replace("Component", ""))))
                    {
                        var c = Activator.CreateInstance(type);
                        _selectedObject!.Components.Add((Component)c!);
                        CloseCurrentPopup();
                    }

                    if (IsItemHovered())
                    {
                        if (BeginTooltip())
                        {
                            var a = type.GetCustomAttribute<ComponentInfoAttribute>();
                            if (a is { Description: not null })
                            {
                                Text($"{a.Description}");
                                Separator();
                            }
                            Text($"Full Name: {type.FullName}");
                            Text($"Assembly:  {type.Assembly.FullName}");
                            EndTooltip();
                        }
                    }
                }
                return;
            }

            if (TreeNode(path[depth]))
            {
                RenderGroupRecursive(path, depth + 1, types);
                TreePop();
            }
        }

        private static int _valI = 0, _valEnum = 0;
        private static bool _valB = false, _valBTemp;
        private static float _valX = 0, _valY = 0, _valZ = 0, _valF = 0;
        private static string _valStr = "";
        private static int _hash;


        private bool _popupOpened;
        #endregion

        private string AddSpacesToCamelCase(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }
            return CamelSpaceRegex().Replace(text, " $1");
        }
        [GeneratedRegex("(?<!^)([A-Z])")]
        private static partial Regex CamelSpaceRegex();
    }
}