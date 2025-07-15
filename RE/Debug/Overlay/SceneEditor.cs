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
using RE.Utils;
using Serilog;
using static ImGuiNET.ImGui;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace RE.Debug.Overlay
{
    internal class SceneEditor : Renderable
    {
        public static SceneEditor Instance = new();
        public static bool Enabled = false;

        public override RenderLayer RenderLayer => RenderLayer.ImGui;
        public override bool IsVisible { get; set; } = false;

        private List<GameObject> _editorObjects = new();
        private Scene _scene;
        private GameObject? _selectedObject;
        private bool _popupOpen = false;

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

            IsVisible = true;
        }

        public void Disable()
        {
            Enabled = false;
            IsVisible = false;
            foreach (var o in _scene.GameObjects)
            {
                foreach (var c in o.Components)
                {
                    c.OnSceneLoading(_scene);
                }
            }
            //SceneManager.LoadScene(_scene);
            // _scene.Dispose(); //todo: do something with thi shi 🥀
        }

        public override void Render(FrameEventArgs args)
        {
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

            ImGuiViewportPtr viewport = ImGui.GetMainViewport();
            float totalWorkWidth = viewport.WorkSize.X;
            float totalWorkHeight = viewport.WorkSize.Y;

            float sidebarWidth = 400;

            Vector2 hierarchyWindowPos = new Vector2(viewport.WorkPos.X + totalWorkWidth - sidebarWidth, viewport.WorkPos.Y);
            Vector2 hierarchyWindowSize = new Vector2(sidebarWidth, totalWorkHeight / 2);

            SetNextWindowPos(hierarchyWindowPos, ImGuiCond.Always);
            SetNextWindowSize(hierarchyWindowSize, ImGuiCond.Always);

            ImGuiWindowFlags flags = ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse;

            Begin("Scene Hierarchy", flags);

            if (Button("remove"))
            {
                _scene.GameObjects.Remove(_selectedObject!);
            }
            if (Button("Save"))
            {
                SceneManager.SaveScene(_scene, "assets/maps/test123");
            }

            Separator();
            foreach (var obj in SceneManager.CurrentScene.GameObjects.Where(s => s.Parent == null))
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
                ImGui.TableSetupColumn("Label");
                ImGui.TableSetupColumn("Value");


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
                TableSetColumnIndex(1);
                {
                    if (Button(_selectedObject.Transform.Position.ToString()))
                    {
                        OpenPopup("prop_new_value");
                        (val_x, val_y, val_z) = _selectedObject.Transform.Position;
                        _p = typeof(Transform).GetProperty("Position")!;
                        hash = _p.GetHashCode();
                    }
                    if (typeof(Transform).GetProperty("Position")?.GetHashCode() == hash)
                        DrawValueChangePopup(_p, _selectedObject.Transform);
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
                        _p = typeof(Transform).GetProperty("Rotation")!;
                        hash = _p.GetHashCode();
                    }
                    if (typeof(Transform).GetProperty("Rotation")!?.GetHashCode() == hash)
                        DrawValueChangePopup(_p, _selectedObject.Transform);
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
                DrawComponents(_selectedObject);


                EndTable();
            }
            End();
        }

        private PropertyInfo _p;
        //refactor_me
        private void DrawComponents(GameObject obj)
        {
            foreach (var com in obj.Components.ToList())
            {
                if (TreeNodeEx(AddSpacesToCamelCase(com.GetType().Name.Replace("Component", "")), ImGuiTreeNodeFlags.OpenOnArrow))
                {
                    if (BeginTable(com.GetHashCode().ToString(), 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                    {
                        TableSetupColumn("##a");
                        TableSetupColumn("##b");

                        TableNextRow();
                        TableSetColumnIndex(0);
                        {
                            float cellWidth = GetContentRegionAvail().X; // ширина доступной области
                            float buttonWidth = CalcTextSize("Reset").X + GetStyle().FramePadding.X * 2; // ширина кнопки

                            SetCursorPosX(GetCursorPosX() + (cellWidth - buttonWidth) / 2f);
                            if (Button("Reset"))
                            {
                                obj.Components.Remove(com);
                                var v = (Component)Activator.CreateInstance(com.GetType())!;
                                obj.Components.Add(v);
                                v.Start();
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

                        foreach (var prop in com.GetType().GetProperties()
                                     .Where(s => s.GetCustomAttribute<EditorProperty>() != null))
                        {
                            EditorProperty attr;
                            TableNextRow();
                            TableSetColumnIndex(0);
                            {
                                attr = prop.GetCustomAttribute<EditorProperty>()!;
                                string name = attr?.DisplayedName ?? AddSpacesToCamelCase(prop.Name);
                                Text(name);
                            }
                            TableSetColumnIndex(1);
                            {
                                if (attr!.IsReadOnly)
                                    Text(prop.GetValue(com)?.ToString() ?? "<null>");
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
                    TreePop();
                }
            }
            Button("Add Component");
        }

        private float val_x = 0, val_y = 0, val_z = 0;
        private string val_str = "";
        private int hash;

        private void DrawValueChangePopup(PropertyInfo prop, object instance)
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
            ImGuiTreeNodeFlags flags = obj.Children.Count == 0
                ? ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen
                : ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.OpenOnDoubleClick;

            bool nodeOpen = TreeNodeEx((nint)obj.GetHashCode(), flags, $"[0x{obj.Id:x}] {obj.Name ?? "<unnamed>"}");

            if (IsItemClicked())
                _selectedObject = obj;

            if (nodeOpen && obj.Children.Count > 0)
            {
                foreach (var child in obj.Children)
                    DrawObjectTree(child);

                TreePop();
            }
        }
        private string AddSpacesToCamelCase(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }
            return Regex.Replace(text, "(?<!^)([A-Z])", " $1");
        }
    }
}
