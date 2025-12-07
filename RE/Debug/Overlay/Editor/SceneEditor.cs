using System.Drawing.Imaging;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Text.RegularExpressions;
using Hexa.NET.ImGui;
using Hexa.NET.ImGuizmo;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core;
using RE.Core.Assets;
using RE.Core.PluginSystem;
using RE.Core.Scripting;
using RE.Core.World;
using RE.Core.World.Components;
using RE.Debug.Overlay.Editor.Panels;
using RE.Rendering;
using RE.Rendering.Renderables;
using RE.Utils;
using Serilog;
using static Hexa.NET.ImGui.ImGui;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;
using PixelFormat = OpenTK.Graphics.OpenGL.PixelFormat;
using Quaternion = OpenTK.Mathematics.Quaternion;
using TkVector3 = OpenTK.Mathematics.Vector3;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;

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

        public static SceneEditor Instance = new();
        public static bool Enabled = false;
        public static bool PreviewLight, PreviewSkybox, ShowAxis = true, ShowGrid = true;
        public static GameObject? SelectedObject;

        private static readonly ImFontPtr _bigFont;
        private static readonly ModelRenderer SelectedObjectOutline = new() { Outline = true };
        private static readonly int LogoImage;

        private static bool _isDockspaceOpen;

        private Scene _scene = null!;
        private GameObject? _selectedObject;
        private List<Type> _customPopups = new();
        private Dictionary<string, List<Type>> _componentDict = new();
        private Node _rootNode = new();
        private HierarchyPanel hierarchyPanel = new HierarchyPanel();
        private InspectorPanel inspectorPanel = new InspectorPanel();
        private AssetBrowserPanel assetBrowserPanel = new AssetBrowserPanel();
        private ViewportPanel viewportPanel = new ViewportPanel();
        private ConsoleWindow consoleWindow = new ConsoleWindow() { Id = "Editor" };


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

                    Color4 color = (Color4)propertyInfo.GetValue(null)!;
                    OpenTK.Mathematics.Vector4 outlineColor = new(color.R, color.G, color.B, color.A);
                    SelectedObjectOutline.OutlineColor = outlineColor;
                }
            };
            string iconPath = Path.GetFullPath($"Assets/RealEngine{(Random.Shared.Next(100) > 50 ? "2" : "")}.ico");

            GL.BindTexture(TextureTarget.Texture2D, LogoImage);

            if (ContentManager.Exists(iconPath))
            {
                using var icon = new Icon(iconPath, new Size(0, 0));

                Size maxSize = new Size(0, 0);
                using Icon tmp = new Icon(iconPath, new Size(512, 512));
                if (tmp.Width > maxSize.Width && tmp.Height > maxSize.Height)
                    maxSize = new Size(tmp.Width, tmp.Height);

                using var bestIcon = new Icon(iconPath, maxSize);
                using Bitmap bmp = bestIcon.ToBitmap();


                var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
                    ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                GL.TexImage2D(TextureTarget.Texture2D,
                    level: 0,
                    internalformat: PixelInternalFormat.Rgba,
                    width: data.Width,
                    height: data.Height,
                    border: 0,
                    format: PixelFormat.Bgra,
                    type: PixelType.UnsignedByte,
                    pixels: data.Scan0);

                bmp.UnlockBits(data);

                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

                GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            }
            else
            {
                LogoImage = (int)Util.CreateMissingTexture(6);
            }
            GL.BindTexture(TextureTarget.Texture2D, 0);

            unsafe
            {
            }
        }

        private void SelectObject(GameObject? obj)
        {
            _selectedObject = obj;
            UpdateSelection();
        }

        public void Enable()
        {
            if (SceneManager.CurrentScene == null!)
            {
                Log.Error("Editor can not be opened if no scene is loaded.");
                return;
            }
            Enabled = true;

            Game.Instance.CursorState = CursorState.Normal;

            Log.Information("Starting Scene Editor for \"{SceneName}\"...", SceneManager.CurrentScene.Name);

            _scene = SceneManager.CurrentScene;
            SelectObject(null);
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
            DrawObjectGizmos();

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

            hierarchyPanel.Draw();
            inspectorPanel.Draw();
            assetBrowserPanel.Draw();
            viewportPanel.Draw();
            consoleWindow.Render(args);
        }

        public static void SetupDockSpace()
        {
            ImGuiIOPtr io = GetIO();

            SetNextWindowPos(new Vector2(0, 0));
            SetNextWindowSize(io.DisplaySize);

            ImGuiWindowFlags windowFlags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize |
                                           ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoBringToFrontOnFocus |
                                           ImGuiWindowFlags.NoBackground;

            PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
            PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);

            ImGui.Begin("MainDockspaceWindow", ref _isDockspaceOpen, windowFlags);

            PopStyleVar(2);

            uint dockspaceId = GetID("EditorDockSpace");

            DockSpace(dockspaceId, new Vector2(0, 0), ImGuiDockNodeFlags.PassthruCentralNode);

            End();
        }

        public void DrawObjectGizmos()
        {
            var pr = (Matrix4x4)Camera.Instance.GetProjectionMatrix();
            var vr = (Matrix4x4)Camera.Instance.GetViewMatrix();
            var one = Matrix4x4.Identity;

            if (_selectedObject != null)
            {
                var rot = _selectedObject!.Transform.Rotation;
                var model =
                    Matrix4x4.CreateScale(_selectedObject!.Transform.Scale.ToSystemVector3())
                    * Matrix4x4.CreateFromQuaternion(rot.ToSystemQuaternion())
                    * Matrix4x4.CreateTranslation(_selectedObject!.Transform.Position.ToSystemVector3());

                if (ImGuizmo.Manipulate(ref vr.M11, ref pr.M11, viewportPanel.Operation, viewportPanel.Mode,
                        ref model.M11))
                {
                    if (Matrix4x4.Decompose(model, out var scale, out var rotation, out var translation))
                    {
                        _selectedObject.SetPosition(translation.ToOpenTkVector3());
                        _selectedObject.SetRotation(rotation.ToOpenTkQuaternion());
                        _selectedObject.Transform.Scale = scale.ToOpenTkVector3();

                        UpdateSelection();
                    }
                }
            }
        }

#pragma warning disable IDE1006
        private OpenTK.Mathematics.Vector3 obj_Position { get => _selectedObject.Transform.Position; set => _selectedObject.SetPosition(value); }
        private Quaternion obj_Rotation { get => _selectedObject.Transform.Rotation; set => _selectedObject.SetRotation(value); }
#pragma warning restore

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


        private object ConvertFromString(string str, Type type)
        {
            if (type == typeof(OpenTK.Mathematics.Vector3))
            {
                var match = Regex.Match(str, @"\(\s*([-+]?\d*\.?\d+)\s*;\s*([-+]?\d*\.?\d+)\s*;\s*([-+]?\d*\.?\d+)\s*\)");
                if (match.Success)
                {
                    float x = float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                    float y = float.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                    float z = float.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
                    return new OpenTK.Mathematics.Vector3(x, y, z);
                }
            }

            if (type == typeof(Vector3))
                return ((OpenTK.Mathematics.Vector3)ConvertFromString(str, typeof(OpenTK.Mathematics.Vector3))!).ToSystemVector3();

            throw new NotImplementedException(type.Name);
        }

        private bool _popupOpened;

        public void UpdateSelection()
        {
            if (_selectedObject == null)
            {
                SelectedObjectOutline.StopRender();
                return;
            }

            var mesh = _selectedObject?.GetComponent<MeshComponent>();

            if (mesh == null! || string.IsNullOrWhiteSpace(mesh.Path))
            {
                SelectedObjectOutline.StopRender();
            }
            else
            {
                SelectedObjectOutline.StartRender();

                SelectedObjectOutline.Position = _selectedObject!.Transform.Position;
                SelectedObjectOutline.Rotation = _selectedObject.Transform.Rotation;
                SelectedObjectOutline.Scale = _selectedObject.Transform.Scale;
                var f = 0.05f;
                SelectedObjectOutline.Scale += (f, f, f);

                if (mesh != null!)
                {
                    SelectedObjectOutline.Path = mesh.Path;
                }
            }
        }
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