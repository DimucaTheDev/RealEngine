using System.Numerics;
using System.Reflection;
using System.Text.RegularExpressions;
using Hexa.NET.ImGui;
using RE.Core;
using RE.Core.Scripting;
using RE.Core.World.Components;
using RE.Debug.Overlay.Editor.PropertyDrawers;
using static Hexa.NET.ImGui.ImGui;
using Vector2 = System.Numerics.Vector2;

namespace RE.Debug.Overlay.Editor.Panels
{
#pragma warning disable IDE0001

    internal partial class InspectorPanel
    {
        private static readonly Dictionary<Type, IPropertyDrawer> Drawers = new()
        {
            { typeof(string), new StringDrawer() },
            { typeof(int), new IntDrawer() },
            { typeof(float), new FloatDrawer() },
            { typeof(bool), new BoolDrawer() },
            { typeof(OpenTK.Mathematics.Vector3), new Vector3Drawer() },
            { typeof(System.Numerics.Vector3), new Vector3Drawer() },
            { typeof(MeshComponent), new MeshComponentDrawer() },
            { typeof(Enum), new EnumDrawer() }
        };
        public static Vector2 ViewportSize = new Vector2(1, 1);
        public void Draw()
        {
            ImGuiViewportPtr viewport = GetMainViewport();
            float totalWorkWidth = viewport.WorkSize.X;
            float totalWorkHeight = viewport.WorkSize.Y;
            float sidebarWidth = 400;

            Vector2 inspectorWindowPos = new Vector2(viewport.WorkPos.X + totalWorkWidth - sidebarWidth, viewport.WorkPos.Y + totalWorkHeight / 2);
            Vector2 inspectorWindowSize = new Vector2(sidebarWidth, totalWorkHeight / 2);

            SetNextWindowPos(inspectorWindowPos, ImGuiCond.FirstUseEver);
            SetNextWindowSize(inspectorWindowSize, ImGuiCond.FirstUseEver);

            Begin($"Inspector"/*, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoResize*/);

            if (SceneEditor.SelectedObject == null)
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
                TableSetColumnIndex(1);

                TableNextRow();
                TableSetColumnIndex(0);
                Text("Name:");
                TableSetColumnIndex(1);
                {
                }
                TableNextRow();
                TableSetColumnIndex(0);
                Text("Position:");
                var f = BindingFlags.NonPublic | BindingFlags.Instance;

                TableSetColumnIndex(1);
                {

                }
                TableNextRow();
                TableSetColumnIndex(0);
                Text("Rotation:");
                TableSetColumnIndex(1);
                {

                }
                TableNextRow();
                TableSetColumnIndex(0);
                Text("Scale:");
                TableSetColumnIndex(1);
                {
                }
                TableNextRow();
                TableSetColumnIndex(0);
                Text("Components:");
                TableSetColumnIndex(1);
                PushItemWidth(-1);
                if (SceneEditor.SelectedObject != null)
                    foreach (var _selectedObject in SceneEditor.SelectedObject.Components)
                    {
                        DrawComponent(_selectedObject);
                    }

                EndTable();
            }
            End();
        }

        /*private void DrawComponents(GameObject obj)
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
        }*/

        public void DrawComponent(Component component)
        {
            if (TreeNodeEx(AddSpacesToCamelCase(component.GetType().Name) + $" ##{component.Owner.Id}", ImGuiTreeNodeFlags.OpenOnArrow))
            {
                float gray = 0.075f;
                float grayAlt = 0.1f;
                PushStyleColor(ImGuiCol.TableRowBg, new Vector4(gray, gray, gray, 1));
                PushStyleColor(ImGuiCol.TableRowBgAlt, new Vector4(grayAlt, grayAlt, grayAlt, 1));

                var props = component.GetType().GetProperties()
                    .Where(p => p.GetCustomAttribute<EditorPropertyAttribute>() != null);

                if (BeginTable("PropertiesTable", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable | ImGuiTableFlags.RowBg))
                {
                    TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed);
                    TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);

                    TableNextRow();
                    TableNextColumn();
                    Button("Reset");
                    TableNextColumn();
                    Button("Remove");


                    foreach (var prop in props)
                    {
                        TableNextRow();

                        TableNextColumn();
                        object val = prop.GetValue(component)!;
                        Text(AddSpacesToCamelCase(prop.Name));

                        TableNextColumn();
                        PushItemWidth(-1);
                        if (Drawers.TryGetValue(prop.PropertyType, out var drawer))
                        {
                            if (drawer.Draw($"##{prop.Name}", ref val, prop))
                            {
                                prop.SetValue(component, val);
                            }
                        }
                        else
                        {
                            Text($"No drawer implemented for type {prop.PropertyType}");
                        }
                    }
                    EndTable();
                }

                PopStyleColor(2);
                TreePop();
            }
        }

        /*    public void DrawValueChangePopup(PropertyInfo prop, object instance)
            {
                // https://youtu.be/hVHEpfgvpCA

                if (BeginPopup("prop_new_value", ImGuiWindowFlags.Modal))
                {
                    Text($"Enter new value");
                    SameLine();
                    TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1.0f), "(?)");

                    Drawers[instance.GetType()]?.Draw(prop.Name, ref , prop);

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
            }*/

        [GeneratedRegex("(?<!^)([A-Z])")]
        private static partial Regex CamelSpaceRegex();
        private string AddSpacesToCamelCase(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }
            return CamelSpaceRegex().Replace(text, " $1");
        }
    }
}
