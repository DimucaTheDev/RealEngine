using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ImGuiNET;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core;
using RE.Core.Scripting;
using RE.Core.World;
using RE.Core.World.Components;
using RE.Rendering;
using RE.Rendering.Renderables;
using RE.Utils;
using Serilog;
using static ImGuiNET.ImGui;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace RE.Debug.Overlay
{
    internal partial class SceneEditor : Renderable
    {
        private class Node
        {
            public string Name;
            public Dictionary<string, Node> Children = new();
            public List<Type> Types = new();
        }

        public static SceneEditor Instance = new();
        public static bool Enabled = false;

        public override RenderLayer RenderLayer => RenderLayer.ImGui;
        public override bool IsVisible { get; set; } = false;

        private List<GameObject> _editorObjects = new();
        private Scene _scene;
        private GameObject? _selectedObject;
        private bool _popupOpen = false;
        private List<Type> _customPopups = new();
        private Dictionary<string, List<Type>> _componentDict = new();
        private Node _rootNode = new();

        private static OpenTK.Mathematics.Vector4 _outlineColor = new(1, 0, 0, 1);
        private static readonly ModelRenderer SelectedObjectOutline = new() { Outline = true };
        private static readonly SpriteRenderer SelectedObjectArrow = new(OpenTK.Mathematics.Vector3.PositiveInfinity, "assets/sprites/editor/arrow_down.png");

        static SceneEditor()
        {
            Variables.VariableChanged += (s, e) =>
            {
                if (s == "selectColor")
                {
                    var propertyInfo = typeof(Color4)
                        .GetProperty(e?.ToString() ?? "red",
                            BindingFlags.IgnoreCase | BindingFlags.Static | BindingFlags.Public)!;
                    if (propertyInfo == null)
                    {
                        var props = typeof(Color4).GetProperties(BindingFlags.Static | BindingFlags.Public)
                            .Where(prop => prop.PropertyType == typeof(Color4));
                        Log.Error($"incorrect color '{e}'. Possible values: {string.Join("; ", props.Select(s => s.Name))}");
                        return;
                    }

                    Color4 color = (Color4)propertyInfo.GetValue(null)!;
                    _outlineColor = new(color.R, color.G, color.B, color.A);
                    SelectedObjectOutline.OutlineColor = _outlineColor;
                }
            };
        }

        public void Enable()
        {
            if (SceneManager.CurrentScene == null!)
            {
                Log.Error("Editor can not be opened if no scene is loaded.");
                return;
            }
            Enabled = true;

            Log.Information($"Starting Scene Editor for \"{SceneManager.CurrentScene.Name}\"...");

            _scene = SceneManager.CurrentScene;//SceneManager.ParseScene(SceneManager.CurrentScene.Name!/*костыль*/); // TODO: set json path to scene's property
            //SceneManager.LoadScene(_scene, true);
            _selectedObject = null;
            IsVisible = true;

            foreach (var type in Assembly.GetExecutingAssembly().GetTypes().Where(t => typeof(IEditorPopup).IsAssignableFrom(t)))
            {
                _customPopups.Add(type);
            }
            _componentDict = Assembly.GetExecutingAssembly()
                .GetTypes()
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
                for (int i = 0; i < pathSegments.Length; i++)
                {
                    string segment = pathSegments[i];
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
        }

        public void Disable()
        {
            Enabled = false;
            IsVisible = false;
            SelectedObjectArrow?.StopRender();
            SelectedObjectOutline?.StopRender();
            if (_scene?.GameObjects != null)
            {
                foreach (var o in _scene?.GameObjects!)
                {
                    foreach (var c in o.Components)
                    {
                        c.OnSceneLoading(_scene);
                    }
                }
            }
            //SceneManager.LoadScene(_scene);
            // _scene.Dispose(); //todo: do something with thi shi 🥀
        }

        public override void Render(FrameEventArgs args)
        {
            if (SelectedObjectArrow != null!)
            {
                if (_selectedObject != null)
                    SelectedObjectArrow.Position = _selectedObject.Transform.Position
                                                   //+ (0, _selectedObject.Transform.Scale.Y, 0)
                                                   + (0, 1.2f, 0)
                                                   + (0, MathF.Sin(Time.ElapsedTime * 3) / 4, 0);
            }

            foreach (var obj in _scene.GameObjects)
            {
                foreach (var com in obj.Components)
                {
                    if (com is ISceneRenderer s)
                        s.DebugRender(args);
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

            ImGuiWindowFlags flags = ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse;

            Begin("Scene Hierarchy", flags);

            if (Button("[+]"))
            {
                var newObject = new GameObject();
                SceneManager.CurrentScene.GameObjects.Add(newObject);
                _selectedObject = newObject;
            }

            if (IsItemHovered())
            {
                BeginTooltip();
                Text("Create new empty object");
                EndTooltip();
            }

            SameLine();

            BeginDisabled(_selectedObject == null);
            if (Button("[-]"))
            {
                _scene.GameObjects.Remove(_selectedObject!);
                _selectedObject = null;
                SelectedObjectArrow!.StopRender();
            }
            if (IsItemHovered())
            {
                BeginTooltip();
                Text("Remove selected object object");
                EndTooltip();
            }
            EndDisabled();

            if (Button("Save"))
            {
                SceneManager.SaveScene(_scene, "assets/maps/test123");
            }

            Separator();
            foreach (var obj in SceneManager.CurrentScene.GameObjects.Where(s => s is { DoNotShowInEditor: false, Parent: null }).ToList())
            {
                DrawObjectTree(obj);
            }
            End();

            Vector2 inspectorWindowPos = new Vector2(viewport.WorkPos.X + totalWorkWidth - sidebarWidth, viewport.WorkPos.Y + totalWorkHeight / 2);
            Vector2 inspectorWindowSize = new Vector2(sidebarWidth, totalWorkHeight / 2);

            SetNextWindowPos(inspectorWindowPos, ImGuiCond.Always);
            SetNextWindowSize(inspectorWindowSize, ImGuiCond.Always);

            DrawInspector(); // 
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
                var id = "0x" + _selectedObject.Id.ToString("x");
                TableSetColumnIndex(1);
                Text(id);

                TableNextRow();
                TableSetColumnIndex(0);
                Text("Name:");
                TableSetColumnIndex(1);
                {
                    if (Button(_selectedObject.Name ?? "<unnamed>"))
                    {
                        OpenPopup("prop_new_value");
                        val_str = _selectedObject.Name ?? "<unnamed>";
                        _p = typeof(GameObject).GetProperty("Name")!;
                        hash = _p.GetHashCode();
                    }
                    if (typeof(GameObject).GetProperty("Name")?.GetHashCode() == hash)
                        DrawValueChangePopup(_p, _selectedObject);
                }
                TableNextRow();
                TableSetColumnIndex(0);
                Text("Position:");
                var f = BindingFlags.NonPublic | BindingFlags.Instance;

                TableSetColumnIndex(1);
                {
                    if (Button(_selectedObject.Transform.Position.ToString()))
                    {
                        OpenPopup("prop_new_value");
                        (val_x, val_y, val_z) = _selectedObject.Transform.Position;
                        _p = GetType().GetProperty("obj_Position", f)!;
                        hash = _p.GetHashCode();
                    }
                    if (GetType().GetProperty("obj_Position", f)?.GetHashCode() == hash)
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
                    if (Button(v.ToString()))
                    {
                        OpenPopup("prop_new_value");
                        (val_x, val_y, val_z) = v;
                        _p = GetType().GetProperty("obj_Rotation", f)!;
                        hash = _p.GetHashCode();
                    }
                    if (GetType().GetProperty("obj_Rotation", f)!?.GetHashCode() == hash)
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
                        (val_x, val_y, val_z) = _selectedObject.Transform.Scale;
                        _p = typeof(Transform).GetProperty("Scale")!;
                        hash = _p.GetHashCode();
                    }
                    if (typeof(Transform).GetProperty("Scale")?.GetHashCode() == hash)
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
        private OpenTK.Mathematics.Vector3 obj_Position { get => _selectedObject.Transform.Position; set => _selectedObject.SetPosition(value); }
        private OpenTK.Mathematics.Quaternion obj_Rotation { get => _selectedObject.Transform.Rotation; set => _selectedObject.SetRotation(value); }

        private PropertyInfo _p;
        //refactor_me
        private void DrawComponents(GameObject obj)
        {
            foreach (var com in obj.Components.ToList())
            {
                if (TreeNodeEx(AddSpacesToCamelCase(com.GetType().Name.Replace("Component", "")),
                        ImGuiTreeNodeFlags.OpenOnArrow))
                {
                    BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                         BindingFlags.Static;

                    foreach (var method in com.GetType().GetMethods(flags)
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
                                com.OnReset();
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

                        foreach (var prop in com.GetType().GetProperties(flags)
                                     .Where(s => s.GetCustomAttribute<EditorPropertyAttribute>() != null))
                        {
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
                                    Checkbox("", ref val_b);
                                    if (val_b != val_b_temp)
                                    {
                                        prop.SetValue(com, val_b);
                                    }

                                    val_b_temp = val_b;
                                }
                                else if (Button(prop.GetValue(com)?.ToString() ?? "<null>"))
                                {
                                    OpenPopup("prop_new_value");
                                    hash = prop.GetHashCode();

                                    //set reference values to prop's value
                                    if (prop.PropertyType == typeof(OpenTK.Mathematics.Vector3))
                                    {
                                        (val_x, val_y, val_z) = ((OpenTK.Mathematics.Vector3)prop.GetValue(com)!);
                                    }

                                    if (prop.PropertyType == typeof(string))
                                        val_str = prop.GetValue(com)?.ToString() ?? "<null>";
                                    if (prop.PropertyType == typeof(float))
                                        val_f = (float)(prop.GetValue(com) ?? 0);
                                    if (prop.PropertyType == typeof(int))
                                        val_i = (int)(prop.GetValue(com) ?? 0);
                                    if (prop.PropertyType == typeof(bool))
                                        val_b = (bool)(prop.GetValue(com) ?? false);
                                }

                                if (IsItemHovered())
                                {
                                    BeginTooltip();
                                    Text(prop.GetValue(com)?.ToString() ?? "<null>");
                                    EndTooltip();
                                }

                                if (!attr!.IsReadOnly && prop.GetHashCode() == hash)
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
                BeginChild("ComponentListChild", new Vector2(200, 300), ImGuiChildFlags.AlwaysAutoResize);

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

        private string _searchComponent;

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
            bool disabled = _selectedObject!.Components.Any(s => s.GetType() == type);
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
                        Text($"Object already contains {type.Name}");
                        Separator();
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

        private static int val_i = 0;
        private static bool val_b = false, val_b_temp;
        private static float val_x = 0, val_y = 0, val_z = 0, val_f = 0;
        private static string val_str = "";
        private static int hash;

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
                        open.Filter = "FBX File|*.fbx";
                        open.ShowDialog();
                        val_str = open.FileName;
                    }
                    SameLine();
                    Text(val_str ?? "");

                    if (Button("Apply"))
                    {
                        prop.SetValue(instance, val_str);
                        CloseCurrentPopup();
                        UpdateSelection();
                    }
                }
                else if (prop.PropertyType == typeof(string) && instance is not MeshComponent or UsableComponent)
                {
                    InputText("##text", ref val_str, 9999);
                    if (Button("Apply"))
                    {
                        prop.SetValue(instance, val_str);
                        CloseCurrentPopup();
                    }
                }
                else if (prop.PropertyType == typeof(OpenTK.Mathematics.Vector3) || prop.PropertyType == typeof(Quaternion))
                {
                    DragFloat("X", ref val_x, 0.05f);
                    DragFloat("Y", ref val_y, 0.05f);
                    DragFloat("Z", ref val_z, 0.05f);

                    if (Button("Apply"))
                    {
                        object val;
                        if (prop.PropertyType != typeof(Quaternion))
                            val = new OpenTK.Mathematics.Vector3(val_x, val_y, val_z);
                        else
                        {
                            var eulerRad = new OpenTK.Mathematics.Vector3(
                                MathHelper.DegreesToRadians(val_x),
                                MathHelper.DegreesToRadians(val_y),
                                MathHelper.DegreesToRadians(val_z));
                            val = Quaternion.FromEulerAngles(eulerRad);
                        }
                        prop.SetValue(instance, val);
                        UpdateSelection();

                        CloseCurrentPopup();
                    }
                }
                else if (prop.PropertyType == typeof(Vector3))
                {
                    DragFloat("X", ref val_x, 0.05f);
                    DragFloat("Y", ref val_y, 0.05f);
                    DragFloat("Z", ref val_z, 0.05f);

                    if (Button("Apply"))
                    {
                        var val = new Vector3(val_x, val_y, val_z);
                        prop.SetValue(instance, val);
                        CloseCurrentPopup();
                        UpdateSelection();

                    }
                }
                else if (prop.PropertyType == typeof(int))
                {
                    var l = prop.GetCustomAttribute<ValueLimitAttribute>();
                    if (l != null)
                        DragInt($"Value[{l.Min}; {l.Max}]:", ref val_i, 1, (int)l.Min, (int)l.Max);
                    else
                        DragInt("Value:", ref val_i, 0.05f, int.MinValue, int.MaxValue);
                    if (Button("Apply"))
                    {
                        prop.SetValue(instance, val_i);
                        CloseCurrentPopup();
                    }
                }
                else if (prop.PropertyType == typeof(float))
                {
                    var l = prop.GetCustomAttribute<ValueLimitAttribute>();
                    if (l != null)
                        DragFloat($"Value[{l.Min}; {l.Max}]:", ref val_f, 0.05f, (float)l.Min, (float)l.Max);
                    else
                        DragFloat("Value:", ref val_f, 0.05f, float.MinValue, float.MaxValue);
                    if (Button("Apply"))
                    {
                        prop.SetValue(instance, val_f);
                        CloseCurrentPopup();
                    }
                }
                else if (prop.PropertyType == typeof(bool))
                {
                    Checkbox("Value:", ref val_b);
                    if (Button("Apply"))
                    {
                        prop.SetValue(instance, val_b);
                        CloseCurrentPopup();
                    }
                }
                else
                {
                    throw new();
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

        private void DrawObjectTree(GameObject obj)
        {
            bool hasVisibleChildren = obj.Children.Any(s => !s.DoNotShowInEditor);

            ImGuiTreeNodeFlags flags = hasVisibleChildren
                ? ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.OpenOnDoubleClick
                : ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;

            bool nodeOpen = TreeNodeEx((nint)obj.GetHashCode(), flags, $"[0x{obj.Id:x}] {obj.Name ?? "<unnamed>"}");

            if (IsItemClicked())
            {
                _selectedObject = obj;
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
            var mesh = _selectedObject.GetComponent<MeshComponent>();

            if (mesh == null!)
            {
                SelectedObjectOutline.StopRender();
                SelectedObjectArrow.Render();
                SelectedObjectArrow.Position = _selectedObject.Transform.Position
                                               + (0, 1.2f, 0)
                                               + (0, MathF.Sin(Time.ElapsedTime * 3) / 4, 0);
            }
            else
            {
                SelectedObjectArrow.StopRender();
                SelectedObjectOutline.Render();

                SelectedObjectOutline.Position = _selectedObject.Transform.Position;
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
