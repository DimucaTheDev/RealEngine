using System.Diagnostics;
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
using Vector4 = System.Numerics.Vector4;

namespace RE.Debug.Overlay
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

        public static SceneEditor Instance = new();
        public static bool Enabled = false;
        public static bool PreviewLight, PreviewSkybox;

        public override RenderLayer RenderLayer => RenderLayer.ImGui;
        public override bool IsVisible { get; set; } = false;

        private Scene _scene = null!;
        private GameObject? _selectedObject;
        private List<Type> _customPopups = new();
        private Dictionary<string, List<Type>> _componentDict = new();
        private Node _rootNode = new();

        private static readonly ImFontPtr _bigFont;
        private static readonly ModelRenderer SelectedObjectOutline = new() { Outline = true };


        private static readonly int LogoImage;

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
                // making big cansolaz font
                /*
                var io = ImGui.GetIO();
                io.Fonts.AddFontDefault();
                _bigFont = io.Fonts.AddFontFromFileTTF("Assets/Fonts/consola.ttf", 60);

                byte* pixels;
                int width, height;
                io.Fonts.TexData
                io.Fonts.GetTexDataAsRGBA32(out pixels, out width, out height, out _);

                int fontTex = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, fontTex);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, width, height,
                    0, PixelFormat.Rgba, PixelType.UnsignedByte, (IntPtr)pixels);

                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

                io.Fonts.SetTexID((IntPtr)fontTex);

                GL.BindTexture(TextureTarget.Texture2D, 0);*/
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

            _scene = SceneManager.CurrentScene;//SceneManager.ParseScene(SceneManager.CurrentScene.Name!/*костыль*/);
                                               // TODO: set json path to scene's property
                                               //SceneManager.LoadScene(_scene, true);
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
                // If the path refers to a group that has types directly, add them
                // This assumes _componentDict stores the types at the *leaf* of the group path
                currentNode.Types.AddRange(entry.Value);
            }

            _translateIcon = LoadTexture("assets/sprites/editor/translate.png");
            _rotateIcon = LoadTexture("assets/sprites/editor/rotate.png");
            _scaleIcon = LoadTexture("assets/sprites/editor/scale.png");
            _worldIcon = LoadTexture("assets/sprites/editor/world.png");
            _localIcon = LoadTexture("assets/sprites/editor/local.png");
        }


        private int _translateIcon, _rotateIcon, _scaleIcon, _worldIcon, _localIcon;
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
            var p = Camera.Instance.Position;

            var speed = 7f * Time.DeltaTime;
            var input = Game.Instance.KeyboardState;
            if (input.IsKeyDown(Keys.W))
                p += (Camera.Instance.Front with { Y = 0 }).Normalized() * speed;
            if (input.IsKeyDown(Keys.S))
                p -= (Camera.Instance.Front with { Y = 0 }).Normalized() * speed;
            if (input.IsKeyDown(Keys.A))
                p -= TkVector3.Normalize(TkVector3.Cross(Camera.Instance.Front, Camera.Instance.Up)) * speed;
            if (input.IsKeyDown(Keys.D))
                p += TkVector3.Normalize(TkVector3.Cross(Camera.Instance.Front, Camera.Instance.Up)) * speed;
            if (input.IsKeyDown(Keys.Space))
                p += TkVector3.UnitY * speed;
            if (input.IsKeyDown(Keys.LeftShift))
                p -= TkVector3.UnitY * speed;

            if (!ImGui.GetIO().WantCaptureKeyboard)
                Camera.Instance.Position = (p);

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

            ImGuiViewportPtr viewport = GetMainViewport();
            float totalWorkWidth = viewport.WorkSize.X;
            float totalWorkHeight = viewport.WorkSize.Y;

            float sidebarWidth = 400;

            Vector2 hierarchyWindowPos = new Vector2(viewport.WorkPos.X + totalWorkWidth - sidebarWidth, viewport.WorkPos.Y);
            Vector2 hierarchyWindowSize = new Vector2(sidebarWidth, totalWorkHeight / 2);

            SetNextWindowPos(hierarchyWindowPos, ImGuiCond.Always);
            SetNextWindowSize(hierarchyWindowSize, ImGuiCond.Always);

            ImGuiWindowFlags flags = ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.MenuBar;

            bool renderAbout = false;
            bool renderQuit = false;

            Begin("Scene Hierarchy", flags);

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
                        Selectable("Render Skybox", ref PreviewSkybox);
                        Selectable("Preview Light", ref PreviewLight);
                        EndMenu();
                    }
                    if (MenuItem("Settings"))
                    { }
                    Separator();
                    if (MenuItem("Exit"))
                    {
                        renderQuit = true;
                    }
                    EndMenu();
                }

                if (BeginMenu("Objects"))
                {
                    if (MenuItem("New Blank"))
                    {
                        var newObject = new GameObject();
                        SceneManager.CurrentScene.GameObjects.Add(newObject);
                        SelectObject(newObject);
                    }
                    if (MenuItem("New Blank 2"))
                    {
                        var newObject = new GameObject();
                        SceneManager.CurrentScene.GameObjects.Add(newObject);

                        var newObject2 = new GameObject();
                        SceneManager.CurrentScene.GameObjects.Add(newObject2);
                        newObject.Parent = newObject2;

                        SelectObject(newObject);
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
                        renderAbout = true;
                    }

                    EndMenu();
                }
                EndMenuBar();
            }



            foreach (var obj in _scene.GameObjects.Where(s => s is { DoNotShowInEditor: false, Parent: null }).ToList())
            {
                DrawObjectTree(obj);
            }
            End();

            if (renderQuit)
            {
                OpenPopup("Quit");
                renderQuit = false;
            }
            if (renderAbout)
            {
                OpenPopup("About");
                renderAbout = false;
            }
            DrawAbout();
            DrawQuit();

            Vector2 inspectorWindowPos = new Vector2(viewport.WorkPos.X + totalWorkWidth - sidebarWidth, viewport.WorkPos.Y + totalWorkHeight / 2);
            Vector2 inspectorWindowSize = new Vector2(sidebarWidth, totalWorkHeight / 2);

            SetNextWindowPos(inspectorWindowPos, ImGuiCond.Always);
            SetNextWindowSize(inspectorWindowSize, ImGuiCond.Always);

            DrawInspector();
        }

        ImGuizmoOperation _operation = ImGuizmoOperation.Translate;
        ImGuizmoMode _mode = ImGuizmoMode.World;
        public void DrawObjectGizmos()
        {
            SetNextWindowPos(new Vector2(10, 10), ImGuiCond.FirstUseEver);

            Begin("##mode_selector", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoSavedSettings);

            var imTextureId1 = new ImTextureID((ulong)_translateIcon);
            var imTextureId2 = new ImTextureID((ulong)_rotateIcon);
            var imTextureId3 = new ImTextureID((ulong)_scaleIcon);
            var imTextureId4 = new ImTextureID((ulong)_worldIcon);
            var imTextureId5 = new ImTextureID((ulong)_localIcon);

            const int size = 20;

            //todo: vinesti peremenii za metod

            BeginDisabled(_operation == ImGuizmoOperation.Translate);
            if (ImageButton("##translate", new ImTextureRef() { TexID = imTextureId1 }, new Vector2(size, size)))
                _operation = ImGuizmoOperation.Translate;
            EndDisabled();

            BeginDisabled(_operation == ImGuizmoOperation.Rotate);
            SameLine();
            if (ImageButton("##rotate", new ImTextureRef() { TexID = imTextureId2 }, new Vector2(size, size)))
                _operation = ImGuizmoOperation.Rotate;
            EndDisabled();

            BeginDisabled(_operation == ImGuizmoOperation.Scale);
            SameLine();
            if (ImageButton("##scale", new ImTextureRef() { TexID = imTextureId3 }, new Vector2(size, size)))
                _operation = ImGuizmoOperation.Scale;
            EndDisabled();

            SameLine();
            if (ImageButton("##mode", new ImTextureRef() { TexID = _mode == ImGuizmoMode.World ? imTextureId4 : imTextureId5 }, new Vector2(size, size)))
                _mode = _mode == ImGuizmoMode.World ? ImGuizmoMode.Local : ImGuizmoMode.World;

            End();
            var pr = (Matrix4x4)Camera.Instance.GetProjectionMatrix();
            var vr = (Matrix4x4)Camera.Instance.GetViewMatrix();
            var one = Matrix4x4.Identity;

            //ImGuizmo.DrawGrid(ref vr, ref pr, ref one, 100);

            if (_selectedObject != null)
            {
                var rot = _selectedObject!.Transform.Rotation;
                var model =
                    Matrix4x4.CreateScale(_selectedObject!.Transform.Scale.ToSystemVector3())
                    * Matrix4x4.CreateFromQuaternion(rot.ToSystemQuaternion())
                    * Matrix4x4.CreateTranslation(_selectedObject!.Transform.Position.ToSystemVector3());


                if (ImGuizmo.Manipulate(ref vr.M11, ref pr.M11, _operation, _mode,
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

        public void DrawQuit()
        {
            if (BeginPopupModal("Quit"))
            {
                Text("Are you sure you want to quit the editor?\nUnsaved changes will be lost.");
                if (Button("Quit without Saving"))
                {
                    CloseCurrentPopup();
                    Disable();
                }
                SameLine();
                if (Button("Save and Quit"))
                {
                    SceneManager.SaveSceneToFile(_scene, $"Assets/Maps/{_scene.Name}");
                    CloseCurrentPopup();
                    Disable();
                }
                SameLine();
                if (Button("Cancel"))
                    CloseCurrentPopup();

                EndPopup();
            }
        }
        private void DrawAbout()
        {
            SetNextWindowSize(new Vector2(500, 400), ImGuiCond.FirstUseEver);
            if (BeginPopupModal("About"))
            {
                Image(new ImTextureRef() { TexID = (IntPtr)LogoImage }, new Vector2(100, 100));
                SameLine();

                float textHeight = ImGui.GetTextLineHeightWithSpacing();
                float imageHeight = 60;
                float yOffset = (imageHeight - textHeight) * 0.5f;

                SetCursorPosY(GetCursorPosY() + yOffset);

                PushFont(_bigFont, 32);
                Text(Game.ProductName);
                PopFont();

                Separator();

                BeginChild("re_info", GetContentRegionAvail(), ImGuiChildFlags.None, ImGuiWindowFlags.AlwaysVerticalScrollbar);

                Text($"Build date: {Game.BuildDate}");
                Text($"Version: {Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyVersionAttribute>()?.Version ?? "<undefined>"}");
                Text($"Commit: {Game.CommitHash}");
                Separator();
                NewLine();
                Text("Small 3D game engine with component-like system.");
                Text("Made by DimucaTheDev with Love");
                NewLine();
                Text("GitHub:");
                SameLine();
                TextLinkOpenURL("DimucaTheDev/RealEngine", "https://github.com/DimucaTheDev/RealEngine");

                //todo: refactor and code cleanup and fix bugs and get a job


                NewLine();
                Separator();
                NewLine();
                Text("Plugins:");
                foreach (var plugin in PluginManager.LoadedPlugins)
                {
                    NewLine();
                    Text($"\t{plugin.PluginInformation.Name} ({plugin.PluginInformation.Version}) by {plugin.PluginInformation.Author ?? "<unknown>"}");
                    if (IsItemHovered())
                    {
                        if (BeginItemTooltip())
                        {
                            Text(plugin.PluginInformation.Assembly.FullName);
                            EndTooltip();
                        }
                    }
                }
                if (Button("Close"))
                    CloseCurrentPopup();
                EndChild();
                EndPopup();
            }
        }

        private void DrawInspector()
        {
            Begin($"Inspector", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoResize);

            if (_selectedObject == null)
            {
                End();
                return;
            }

            if (BeginTable("InspectorTable", 2, ImGuiTableFlags.BordersOuterH | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
            {
                TableSetupColumn("Label");
                TableSetupColumn("Value");


                TableNextRow();
                TableSetColumnIndex(0);
                Text("Id:");
                var id = $"0x{_selectedObject.Id:x} ({_selectedObject.Id})";
                TableSetColumnIndex(1);
                Text(id);

                TableNextRow();
                TableSetColumnIndex(0);
                Text("Name:");
                TableSetColumnIndex(1);
                {
                    if (Button((_selectedObject.Name ?? "<unnamed>") + "##name"))
                    {
                        OpenPopup("prop_new_value");
                        _valStr = _selectedObject.Name ?? "<unnamed>";
                        _p = typeof(GameObject).GetProperty("Name")!;
                        _hash = _p.GetHashCode();
                    }
                    if (typeof(GameObject).GetProperty("Name")?.GetHashCode() == _hash)
                        DrawValueChangePopup(_p, _selectedObject);
                }
                TableNextRow();
                TableSetColumnIndex(0);
                Text("Position:");
                var f = BindingFlags.NonPublic | BindingFlags.Instance;

                TableSetColumnIndex(1);
                {
                    if (Button(_selectedObject.Transform.Position + "##pos"))
                    {
                        OpenPopup("prop_new_value");
                        (_valX, _valY, _valZ) = _selectedObject.Transform.Position;
                        _p = GetType().GetProperty(nameof(obj_Position), f)!;
                        _hash = _p.GetHashCode();
                    }
                    if (GetType().GetProperty(nameof(obj_Position), f)?.GetHashCode() == _hash)
                        DrawValueChangePopup(_p, this);
                }
                TableNextRow();
                TableSetColumnIndex(0);
                Text("Rotation:");
                TableSetColumnIndex(1);
                {
                    var transformRotation = _selectedObject.Transform.Rotation;
                    var v = new OpenTK.Mathematics.Vector3(MathHelper.RadiansToDegrees(transformRotation.X),
                        MathHelper.RadiansToDegrees(transformRotation.Y),
                        MathHelper.RadiansToDegrees(radians: transformRotation.Z));
                    if (Button(v + "##rot"))
                    {
                        OpenPopup("prop_new_value");
                        (_valX, _valY, _valZ) = v;
                        _p = GetType().GetProperty(nameof(obj_Rotation), f)!;
                        _hash = _p.GetHashCode();
                    }
                    if (GetType().GetProperty(nameof(obj_Rotation), f)!?.GetHashCode() == _hash)
                        DrawValueChangePopup(_p, this);
                }
                TableNextRow();
                TableSetColumnIndex(0);
                Text("Scale:");
                TableSetColumnIndex(1);
                {
                    if (Button(_selectedObject.Transform.Scale.ToString()))
                    {
                        OpenPopup("prop_new_value");
                        (_valX, _valY, _valZ) = _selectedObject.Transform.Scale;
                        _p = typeof(Transform).GetProperty(nameof(Transform.Scale))!;
                        _hash = _p.GetHashCode();
                    }
                    if (typeof(Transform).GetProperty(nameof(Transform.Scale))?.GetHashCode() == _hash)
                        DrawValueChangePopup(_p, _selectedObject.Transform);
                }
                TableNextRow();
                TableSetColumnIndex(0);
                Text("Components:");
                TableSetColumnIndex(1);

                if (_selectedObject != null)
                    DrawComponents(_selectedObject);

                EndTable();
            }
            End();
        }

#pragma warning disable IDE1006
        private OpenTK.Mathematics.Vector3 obj_Position { get => _selectedObject.Transform.Position; set => _selectedObject.SetPosition(value); }
        private OpenTK.Mathematics.Quaternion obj_Rotation { get => _selectedObject.Transform.Rotation; set => _selectedObject.SetRotation(value); }
#pragma warning restore

        private PropertyInfo _p = null!;

        //refactor_me
        private void DrawComponents(GameObject obj)
        {
            foreach (var com in obj.Components.ToList())
            {
                var type = com.GetType();
                if (TreeNodeEx(AddSpacesToCamelCase(type.Name.Replace("Component", "")),
                        ImGuiTreeNodeFlags.OpenOnArrow))
                {
                    BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                         BindingFlags.Static;

                    foreach (var method in type.GetMethods(flags)
                                 .Where(s => s.GetCustomAttribute<EditorButtonAttribute>() != null))
                    {
                        if (Button(AddSpacesToCamelCase(method.GetCustomAttribute<EditorButtonAttribute>()?.ShownText ??
                                                        method.Name)))
                        {
                            method.Invoke(com, null);
                        }
                    }

                    if (BeginTable(com.GetHashCode().ToString(), 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                    {
                        TableSetupColumn("##a");
                        TableSetupColumn("##b");

                        TableNextRow();
                        TableSetColumnIndex(0);
                        {
                            float cellWidth = GetContentRegionAvail().X; // ширина доступной области
                            float buttonWidth =
                                CalcTextSize("Reset").X + GetStyle().FramePadding.X * 2; // ширина кнопки

                            SetCursorPosX(GetCursorPosX() + (cellWidth - buttonWidth) / 2f);
                            if (Button("Reset"))
                            {
                                obj.Components.Remove(com);
                                var newCom = (Component)Activator.CreateInstance(type)!;
                                obj.Components.Add(newCom);
                            }
                        }
                        TableSetColumnIndex(1);
                        {
                            float cellWidth = GetContentRegionAvail().X;
                            float buttonWidth = CalcTextSize("Remove").X + GetStyle().FramePadding.X * 2;

                            SetCursorPosX(GetCursorPosX() + (cellWidth - buttonWidth) / 2f);
                            if (Button("Remove"))
                            {
                                obj.Components.Remove(com);
                                com.OnDestroy(); // ?
                            }
                        }

                        foreach (var prop in type.GetProperties(flags)
                                     .Where(s => s.GetCustomAttribute<EditorPropertyAttribute>() != null))
                        {
                            var ifs = prop.GetCustomAttributes<IfAttribute>();
                            if (ifs.Any())
                            {
                                if (!ifs.All(att =>
                                    {
                                        try
                                        {
                                            var propInfo = type.GetProperty(att.Name)!;
                                            var propValue = propInfo.GetValue(com);
                                            var expected = Convert.ChangeType(att.Value, propInfo.PropertyType);

                                            return Equals(propValue, expected);
                                        }
                                        catch
                                        {
                                            Log.Error("{AttributeName} links to property {Name} that does not exist!", nameof(IfAttribute), att.Name);
                                            throw;
                                        }
                                    }))
                                {
                                    continue;
                                }
                            }
                            var ifns = prop.GetCustomAttributes<IfNotAttribute>();
                            if (ifns.Any())
                            {
                                if (!ifns.All(att =>
                                    {
                                        try
                                        {
                                            var propInfo = type.GetProperty(att.Name)!;
                                            var propValue = propInfo.GetValue(com);
                                            var expected = Convert.ChangeType(att.Value, propInfo.PropertyType);

                                            return !Equals(propValue, expected);
                                        }
                                        catch
                                        {
                                            Log.Error("{AttributeName} links to property {Name} that does not exist!", nameof(IfNotAttribute), att.Name);
                                            throw;
                                        }
                                    }))
                                {
                                    continue;
                                }
                            }

                            EditorPropertyAttribute attr;
                            TableNextRow();
                            TableSetColumnIndex(0);
                            {
                                attr = prop.GetCustomAttribute<EditorPropertyAttribute>()!;
                                string name = attr?.DisplayedName ?? AddSpacesToCamelCase(prop.Name);
                                Text(name);
                            }
                            TableSetColumnIndex(1);
                            {
                                if (attr!.IsReadOnly)
                                    Text(prop.GetValue(com)?.ToString() ?? "<null>");
                                else if (prop.PropertyType == typeof(bool))
                                {
                                    Checkbox("", ref _valB);
                                    if (_valB != _valBTemp)
                                    {
                                        prop.SetValue(com, _valB);
                                    }

                                    _valBTemp = _valB;
                                }
                                else if (Button((prop.GetValue(com)?.ToString() ?? "<null>") + $"##id_{prop.Name}"))
                                {
                                    OpenPopup("prop_new_value");
                                    _hash = prop.GetHashCode();

                                    //set reference values to prop's value
                                    if (prop.PropertyType == typeof(OpenTK.Mathematics.Vector3))
                                    {
                                        (_valX, _valY, _valZ) = ((OpenTK.Mathematics.Vector3)prop.GetValue(com)!);
                                    }

                                    if (prop.PropertyType == typeof(string))
                                        _valStr = prop.GetValue(com)?.ToString() ?? "<null>";
                                    if (prop.PropertyType == typeof(float))
                                        _valF = (float)(prop.GetValue(com) ?? 0);
                                    if (prop.PropertyType == typeof(int))
                                        _valI = (int)(prop.GetValue(com) ?? 0);
                                    if (prop.PropertyType == typeof(bool))
                                        _valB = (bool)(prop.GetValue(com) ?? false);
                                }

                                if (IsItemHovered())
                                {
                                    BeginTooltip();
                                    Text(prop.GetValue(com)?.ToString() ?? "<null>");
                                    EndTooltip();
                                }

                                if (!attr!.IsReadOnly && prop.GetHashCode() == _hash)
                                    DrawValueChangePopup(prop, com);
                            }
                        }

                        EndTable();
                    }


                    if (com is IEditorPopup popup)
                    {
                        var s = popup.GetPopupSettings();
                        SetNextWindowSize(new Vector2(s.Width, s.Height));

                        PushStyleColor(ImGuiCol.Border, (Vector4)new OpenTK.Mathematics.Vector4(1f, 1f, 1f, 1f)); // белый
                        PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1.0f);

                        if (popup.ShouldRenderPopup() && Begin(s.Title))
                        {
                            popup.RenderPopup();
                            End();
                        }

                        PopStyleVar();
                        PopStyleColor();
                    }
                    TreePop();
                }
            }

            if (Button("Add Component"))
            {
                OpenPopup("new_component");
                _searchComponent = "";
            }
            if (BeginPopup("new_component"))
            {
                BeginChild("ComponentListChild", new Vector2(200, 300),
                    ImGuiChildFlags.AlwaysAutoResize | ImGuiChildFlags.AutoResizeX | ImGuiChildFlags.AutoResizeY);

                float fullWidth = GetContentRegionAvail().X;
                PushItemWidth(fullWidth);
                InputText("", ref _searchComponent, 512);
                PopItemWidth();

                Separator();
                if (string.IsNullOrWhiteSpace(_searchComponent))
                {
                    RenderNodeRecursive(_rootNode);
                }
                else
                {
                    foreach (var c in _componentDict.Values)
                        foreach (var type in c.Where(s => s.Name.Contains(_searchComponent, StringComparison.InvariantCultureIgnoreCase)))
                        {
                            DrawButton(type);
                        }
                }

                EndChild();
                EndPopup();
            }
        }

        private string _searchComponent = null!;

        void RenderNodeRecursive(Node node)
        {
            foreach (var childEntry in node.Children.OrderBy(c => c.Key))
            {
                Node childNode = childEntry.Value;
                if (TreeNode(childNode.Name))
                {
                    RenderNodeRecursive(childNode);
                    TreePop();
                }
            }
            foreach (var type in node.Types.OrderBy(t => t.Name))
            {
                DrawButton(type);
            }
        }

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

        public void DrawValueChangePopup(PropertyInfo prop, object instance)
        {
            // https://youtu.be/hVHEpfgvpCA

            if (BeginPopup("prop_new_value", ImGuiWindowFlags.Modal))
            {
                Text($"Enter new value");
                SameLine();
                TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1.0f), "(?)");

                if (IsItemHovered())
                {
                    BeginTooltip();
                    Text($"Property Name: {prop.Name}");
                    Text($"Component: {instance.GetType().FullName}");
                    EndTooltip();
                }

                if (instance.GetType() == typeof(MeshComponent))
                {
                    if (Button("Browse..."))
                    {
                        var open = new OpenFileDialog() { Title = "Select 3D Model", AddExtension = true, CheckFileExists = true, };
                        open.CustomPlaces.Add(Path.GetFullPath("assets/models"));
                        open.CustomPlaces.Add(Path.GetFullPath("assets/maps"));
                        open.InitialDirectory = Path.GetFullPath("assets/models");
                        open.Multiselect = false;
                        open.Filter = "FBX File|*.fbx|SMDL File|*.smdl";
                        open.ShowDialog();
                        _valStr = Path.GetRelativePath(".", open.FileName);
                    }
                    SameLine();
                    Text(_valStr ?? "");

                    if (Button("Apply"))
                    {
                        prop.SetValue(instance, _valStr);
                        CloseCurrentPopup();
                        UpdateSelection();
                    }
                }
                else if (prop.PropertyType == typeof(string) && instance is not MeshComponent or UsableComponent)
                {
                    InputText("##text", ref _valStr, 9999);
                    if (Button("Apply"))
                    {
                        prop.SetValue(instance, _valStr);
                        CloseCurrentPopup();
                    }
                }
                else if (prop.PropertyType == typeof(OpenTK.Mathematics.Vector3) || prop.PropertyType == typeof(Quaternion))
                {
                    DragFloat("X", ref _valX, 0.05f);
                    DragFloat("Y", ref _valY, 0.05f);
                    DragFloat("Z", ref _valZ, 0.05f);

                    if (Button("Apply"))
                    {
                        object val;
                        if (prop.PropertyType != typeof(Quaternion))
                            val = new OpenTK.Mathematics.Vector3(_valX, _valY, _valZ);
                        else
                        {
                            var eulerRad = new OpenTK.Mathematics.Vector3(
                                MathHelper.DegreesToRadians(_valX),
                                MathHelper.DegreesToRadians(_valY),
                                MathHelper.DegreesToRadians(_valZ));
                            val = Quaternion.FromEulerAngles(eulerRad);
                        }
                        prop.SetValue(instance, val);
                        UpdateSelection();

                        CloseCurrentPopup();
                    }
                }
                else if (prop.PropertyType == typeof(Vector3))
                {
                    DragFloat("X", ref _valX, 0.05f);
                    DragFloat("Y", ref _valY, 0.05f);
                    DragFloat("Z", ref _valZ, 0.05f);

                    if (Button("Apply"))
                    {
                        var val = new Vector3(_valX, _valY, _valZ);
                        prop.SetValue(instance, val);
                        CloseCurrentPopup();
                        UpdateSelection();

                    }
                }
                else if (prop.PropertyType == typeof(int))
                {
                    var l = prop.GetCustomAttribute<ValueLimitAttribute>();
                    if (l != null)
                        DragInt($"Value[{l.Min}; {l.Max}]:", ref _valI, 1, (int)l.Min, (int)l.Max);
                    else
                        DragInt("Value:", ref _valI, 0.05f, int.MinValue, int.MaxValue);
                    if (Button("Apply"))
                    {
                        prop.SetValue(instance, _valI);
                        CloseCurrentPopup();
                    }
                }
                else if (prop.PropertyType == typeof(float))
                {
                    var l = prop.GetCustomAttribute<ValueLimitAttribute>();
                    if (l != null)
                        DragFloat($"Value[{l.Min}; {l.Max}]:", ref _valF, (float)l.Step, (float)l.Min, (float)l.Max);
                    else
                        DragFloat("Value:", ref _valF, (float)l.Step, float.MinValue, float.MaxValue);
                    if (Button("Apply"))
                    {
                        prop.SetValue(instance, _valF);
                        CloseCurrentPopup();
                    }
                }
                else if (prop.PropertyType.IsEnum)
                {
                    var values = Enum.GetValues(prop.PropertyType);
                    var names = Enum.GetNames(prop.PropertyType);

                    int currentIndex = _valEnum;

                    if (BeginCombo("Value:", names[currentIndex]))
                    {
                        for (int i = 0; i < values.Length; i++)
                        {
                            bool isSelected = i == currentIndex;
                            if (Selectable(names[i], isSelected))
                            {
                                currentIndex = i;
                                _valEnum = (int)(values.GetValue(i)!);
                            }

                            if (isSelected)
                                SetItemDefaultFocus();
                        }
                        EndCombo();
                    }

                    if (Button("Apply"))
                    {
                        prop.SetValue(instance, _valEnum);
                        CloseCurrentPopup();
                    }
                }


                SameLine();
                if (Button("Cancel"))
                {
                    CloseCurrentPopup();
                }

                EndPopup();
            }
        }

        //todo
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

            if (type == typeof(Vector3)) //Sys.Math
                return ((OpenTK.Mathematics.Vector3)ConvertFromString(str, typeof(OpenTK.Mathematics.Vector3))!).ToSystemVector3();

            throw new NotImplementedException(type.Name);
        }

        private bool _popupOpened;
        private void DrawObjectTree(GameObject obj)
        {
            bool hasVisibleChildren = obj.Children.Any(s => !s.DoNotShowInEditor);
            ImGuiTreeNodeFlags flags = hasVisibleChildren
                ? ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.OpenOnDoubleClick | ImGuiTreeNodeFlags.DrawLinesFull
                : ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;

            bool nodeOpen = TreeNodeEx(obj.GetHashCode().ToString(), flags, $"{obj.Name ?? "<unnamed>"}");

            PushStyleColor(ImGuiCol.PopupBg, new Vector4(0.1f, 0.1f, 0.1f, 0.95f));
            if (BeginPopupContextItem())
            {
                if (Selectable("Delete Object"))
                {
                    _scene.GameObjects.Remove(obj!);
                    _selectedObject = null;
                    UpdateSelection();
                }
                if (IsItemHovered())
                    _popupOpened = true;
                if (_popupOpened && !IsItemHovered())
                {
                    CloseCurrentPopup();
                    _popupOpened = false;
                }
                EndPopup();
            }
            PopStyleColor();

            if (IsItemClicked())
            {
                SelectObject(obj);
                UpdateSelection();
            }

            if (hasVisibleChildren && nodeOpen)
            {
                foreach (var child in obj.Children.Where(s => !s.DoNotShowInEditor))
                    DrawObjectTree(child);

                TreePop();
            }
        }
        void UpdateSelection()
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

        private int LoadTexture(string path)
        {
            int t = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, t);

            var pathToFace = path;

            using var image = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(pathToFace);
            //  image.Mutate(x => x.Flip(FlipMode.Vertical));
            var pixels = new byte[4 * image.Width * image.Height];
            image.CopyPixelDataTo(pixels);

            GL.TexImage2D(TextureTarget.Texture2D, 0,
                PixelInternalFormat.Rgba,
                image.Width, image.Height, 0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                pixels);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            return t;
        }


        [GeneratedRegex("(?<!^)([A-Z])")]
        private static partial Regex CamelSpaceRegex();
    }
}