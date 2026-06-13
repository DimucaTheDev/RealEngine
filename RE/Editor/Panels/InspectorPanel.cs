using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Hexa.NET.ImGui;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using RE.Core;
using RE.Core.Assets;
using RE.Core.Scripting.Attributes;
using RE.Core.World.Components;
using RE.Editor.PropertyDrawers;
using RE.Rendering;
using RE.Rendering.Renderables;
using RE.Rendering.Texturing;
using RE.Utils;
using Serilog;
using static Hexa.NET.ImGui.ImGui;
using Vector2 = System.Numerics.Vector2;
using Vector3 = OpenTK.Mathematics.Vector3;

namespace RE.Editor.Panels
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
            { typeof(Vector3), new Vector3Drawer() },
            { typeof(System.Numerics.Vector3), new Vector3Drawer() },
            { typeof(MeshComponent), new MeshComponentDrawer() },
            { typeof(Enum), new EnumDrawer() }
        };

        private ModelRenderer? _materialPreviewModel;
        private ModelRenderer? _materialPreviewFloor;
        private readonly Camera _materialPreviewCamera = new(new Vector3(-5, 2, 0), new Vector3(0, 1, 0), 200, 200);
        private Vector2 _materialPreviewSize;
        private int _materialPreviewFboId;
        private int _materialPreviewTextureId;
        private int _materialPreviewRboId;
        private string _componentSearchString = "";
        private List<Type> _components = [];

        private readonly Texture _cubeModelButtonStaticTexture =
            new StaticTexture("assets/editor/sprites/previewCube.png");

        private readonly Texture _sphereModelButtonStaticTexture =
            new StaticTexture("assets/editor/sprites/previewSphere.png");

        public void Draw()
        {
            ImGuiViewportPtr viewport = GetMainViewport();
            float totalWorkWidth = viewport.WorkSize.X;
            float totalWorkHeight = viewport.WorkSize.Y;
            float sidebarWidth = 400;

            Vector2 inspectorWindowPos = new Vector2(viewport.WorkPos.X + totalWorkWidth - sidebarWidth,
                viewport.WorkPos.Y + totalWorkHeight / 2);
            Vector2 inspectorWindowSize = new Vector2(sidebarWidth, totalWorkHeight / 2);

            //SetNextWindowPos(inspectorWindowPos, ImGuiCond.FirstUseEver);
            //SetNextWindowSize(inspectorWindowSize, ImGuiCond.FirstUseEver);

            SetNextWindowPos(new Vector2(1512, 27), ImGuiCond.FirstUseEver);
            SetNextWindowSize(new Vector2(400, 974), ImGuiCond.FirstUseEver);

            Begin(
                "Inspector" /*, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoResize*/);

            if (SceneEditor.SelectedObject == null)
            {
                Text("Todo: Scene settings");
                End();
                return;
            }

            SetNextWindowSize(new Vector2(0, GetWindowSize().Y - 400));
            BeginChild("Props");
            if (BeginTable("InspectorTable", 2,
                    ImGuiTableFlags.BordersOuterH | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.RowBg |
                    ImGuiTableFlags.SizingStretchProp))
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
                    Text(SceneEditor.SelectedObject.Name ?? "null");
                }
                TableNextRow();
                TableSetColumnIndex(0);
                Text("Position:");
                var f = BindingFlags.NonPublic | BindingFlags.Instance;

                TableSetColumnIndex(1);
                {
                    Text(SceneEditor.SelectedObject.Transform.Position.ToString());
                }
                TableNextRow();
                TableSetColumnIndex(0);
                Text("Rotation:");
                TableSetColumnIndex(1);
                {
                    Text($"{SceneEditor.SelectedObject.Transform.RotationXyz}");
                }
                TableNextRow();
                TableSetColumnIndex(0);
                Text("Scale:");
                TableSetColumnIndex(1);
                {
                    Text(SceneEditor.SelectedObject.Transform.Scale.ToString());
                }
                TableNextRow();
                TableSetColumnIndex(0);
                Text("Components:");
                TableSetColumnIndex(1);
                PushItemWidth(-1);
                if (SceneEditor.SelectedObject != null)
                    foreach (var selectedObject in SceneEditor.SelectedObject.Components)
                    {
                        DrawComponent(selectedObject);
                    }

                if (Button("Add Component"))
                    _componentSearchString = "";

                if (BeginPopupContextItem(ImGuiPopupFlags.MouseButtonLeft))
                {
                    if (InputText("##search", ref _componentSearchString, 128))
                    {
                        _components = Assembly.GetExecutingAssembly().GetTypes()
                            .Where(s => s != typeof(Component) && s.IsAssignableTo(typeof(Component))).ToList();
                    }

                    Separator();
                    foreach (var c in _components)
                    {
                        if (Button(SplitComponentName(c.Name)))
                        {
                            SceneEditor.SelectedObject.Components.Add((Component)Activator.CreateInstance(c));
                        }

                        if (IsItemHovered())
                        {
                            if (BeginTooltip())
                            {
                                Text(c.FullName);
                                EndTooltip();
                            }
                        }
                    }

                    EndPopup();
                }

                EndTable();
            }

            EndChild();

            MaterialWindow();

            End();
        }

        public static string SplitComponentName(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            const string suffix = "Component";

            if (input.EndsWith(suffix))
                input = input.Substring(0, input.Length - suffix.Length);

            var result = new StringBuilder();
            result.Append(input[0]);

            for (int i = 1; i < input.Length; i++)
            {
                char c = input[i];

                if (char.IsUpper(c) && input[i - 1] != ' ')
                    result.Append(' ');

                result.Append(c);
            }

            return result.ToString();
        }

        private void SetupSceneFbo(int width, int height)
        {
            if (width <= 0 || height <= 0) return;

            if (_materialPreviewFboId != 0)
            {
                GL.DeleteFramebuffer(_materialPreviewFboId);
                GL.DeleteTexture(_materialPreviewTextureId);
                GL.DeleteRenderbuffer(_materialPreviewRboId);
            }

            GL.GenFramebuffers(1, out _materialPreviewFboId);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _materialPreviewFboId);

            GL.GenTextures(1, out _materialPreviewTextureId);
            GL.BindTexture(TextureTarget.Texture2D, _materialPreviewTextureId);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, width, height, 0, PixelFormat.Rgba,
                PixelType.UnsignedByte, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                (int)TextureMagFilter.Linear);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                TextureTarget.Texture2D, _materialPreviewTextureId, 0);

            GL.GenRenderbuffers(1, out _materialPreviewRboId);
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _materialPreviewRboId);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.Depth24Stencil8, width, height);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment,
                RenderbufferTarget.Renderbuffer, _materialPreviewRboId);

            if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
            {
                Log.Error("{Method}: GL Framebuffer is not complete", nameof(SetupSceneFbo));
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        private Vector4 _baseColor = Vector4.One;
        private float _shininess = 32; //todo: change whole material, so we dont have to manually set defaults

        private void MaterialWindow()
        {
            const string boxModelPath = "assets/models/cub.smdl";
            const string sphereModelPath = "assets/models/krug.fbx";

            if (SceneEditor.SelectedObject!.GetComponent<MeshComponent>() == null) return;

            SetNextWindowSize(new Vector2(0, 0), ImGuiCond.Always);
            BeginChild("materialSettings", ImGuiChildFlags.Borders);
            Text("Material Settings");


            if (_materialPreviewModel == null)
            {
                _materialPreviewModel = ModelRenderer.Create(boxModelPath);
                _materialPreviewModel.Model.SetTexture(StaticTexture.CreateMonoColorTexture(_baseColor));
                _materialPreviewModel.IgnoreLight = true;
            }

            _materialPreviewModel.Model.Material =
                SceneEditor.SelectedObject!.GetComponent<MeshComponent>()!.ModelRenderer.Model.Material;

            if (_materialPreviewFloor == null)
            {
                _materialPreviewFloor = ModelRenderer.Create(boxModelPath);
                _materialPreviewFloor.Model.SetTexture(
                    StaticTexture.CreateMissingTexture(48, [110, 110, 110, 255], [35, 35, 35, 255]));
                _materialPreviewFloor.IgnoreLight = true;
            }

            var treeFlags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.OpenOnArrow;

            if (TreeNodeEx("Preview", treeFlags))
            {
                BeginChild("mpreview");

                GL.GetInteger(GetPName.FramebufferBinding, out var oldBuf);
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, _materialPreviewFboId);
                GL.Viewport(0, 0, (int)_materialPreviewSize.X, (int)_materialPreviewSize.Y);
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                GL.DepthFunc(DepthFunction.Lequal);
                GL.Enable(EnableCap.DepthTest);
                GL.Enable(EnableCap.Blend);
                GL.ClearColor(Color.CadetBlue);


                var matrixModel = Matrix4.CreateTranslation(0, 0, 0) *
                                  Matrix4.CreateRotationY(
                                      MathHelper.DegreesToRadians(180) * MathF.Sin(Time.ElapsedTime));
                var matrixFloor = Matrix4.CreateScale((10, 0.1f, 10)) *
                                  Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(30)) *
                                  Matrix4.CreateTranslation(3, -1, 0);
                _materialPreviewCamera.Fov = 40;
                _materialPreviewCamera.Front = new Vector3(1, -0.425f, 0).Normalized();
                _materialPreviewModel.Render(matrixModel, _materialPreviewCamera);
                _materialPreviewFloor.Render(matrixFloor, _materialPreviewCamera);

                var w = Camera.GetActiveCamera().RenderWidth;
                var h = Camera.GetActiveCamera().RenderHeight;
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, oldBuf);
                GL.Viewport(0, 0, w, h);


                var r = GetContentRegionAvail();
                r.X -= 40;
                if (r != _materialPreviewSize)
                {
                    _materialPreviewSize = r;
                    SetupSceneFbo((int)_materialPreviewSize.X, (int)_materialPreviewSize.Y);
                    _materialPreviewCamera.RenderWidth = (int)_materialPreviewSize.X;
                    _materialPreviewCamera.RenderHeight = (int)_materialPreviewSize.Y;
                }

                var cursor = GetCursorPos();

                Image(new ImTextureRef { TexID = new ImTextureID(_materialPreviewTextureId) }, _materialPreviewSize,
                    new Vector2(1, 1),
                    new Vector2(0, 0));

                void ModelButton(ImTextureRef texture, string path)
                {
                    SetCursorPosX(cursor.X + _materialPreviewSize.X + 7);
                    if (ImageButton(path, texture, new Vector2(24, 24)))
                    {
                        _materialPreviewModel.Model = AssetCache.Get(path, ModelLoader.DefaultModelCacheFactory);
                        _materialPreviewModel.Model.Material = SceneEditor.SelectedObject!.GetComponent<MeshComponent>()!
                            .ModelRenderer.Model.Material;
                    }
                }

                SetCursorPos(cursor with { X = cursor.X + _materialPreviewSize.X + 7 });

                {
                    SetCursorPosX(cursor.X + _materialPreviewSize.X + 7);
                    if (ImageButton("##same", _cubeModelButtonStaticTexture, new Vector2(24, 24)))
                    {
                        _materialPreviewModel.Model = SceneEditor.SelectedObject.GetComponent<MeshComponent>()!
                            .ModelRenderer.Model;
                    }
                }
                ModelButton(_cubeModelButtonStaticTexture, boxModelPath);
                ModelButton(_sphereModelButtonStaticTexture, sphereModelPath);

                EndChild();
                TreePop();
            }

            if (TreeNodeEx("Surface", treeFlags))
            {
                BeginChild("surface");
                if (ColorEdit4("Base Color", ref _baseColor.X))
                {
                    _materialPreviewModel.Model.SetTexture(StaticTexture.CreateMonoColorTexture(_baseColor), true);
                }
                //todo: staticTexture as surface color

                SliderFloat("Shininess", ref _shininess, 0.1f, 100);

                EndChild();
                TreePop();
            }

            _materialPreviewModel.Model.Material.Data.Shininess = _shininess;


            EndChild();
        }
        /*private void DrawComponents(GameObject obj)
        {
            foreach (var com in obj.Components.ToList())
            {
                var type = com.GetType();
                if (TreeNodeEx(AddSpacesToCamelCase(type.Name.Replace("Component", "")),
                        ImGuiTreeNodeFlags.OpenOnArrow))
                {
                    BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Main |
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
            if (TreeNodeEx(AddSpacesToCamelCase(component.GetType().Name) + $" ##{component.Owner.Id}",
                    ImGuiTreeNodeFlags.OpenOnArrow))
            {
                float gray = 0.075f;
                float grayAlt = 0.1f;
                PushStyleColor(ImGuiCol.TableRowBg, new System.Numerics.Vector4(gray, gray, gray, 1));
                PushStyleColor(ImGuiCol.TableRowBgAlt, new System.Numerics.Vector4(grayAlt, grayAlt, grayAlt, 1));

                var props = component.GetType().GetProperties()
                    .Where(p => p.GetCustomAttribute<EditorPropertyAttribute>() != null);

                if (BeginTable("PropertiesTable", 2,
                        ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable | ImGuiTableFlags.RowBg))
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
                                try
                                {
                                    prop.SetValue(component, val);
                                }
                                catch (Exception e)
                                {
                                    Log.Error(e, "Unable to update property({Name}) value({Value})", prop.Name, val);
                                }
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