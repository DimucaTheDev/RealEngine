using System.Numerics;
using ImGuiNET;
using OpenTK.Windowing.Common;
using RE.Core.World;
using RE.Rendering;
using Serilog;
using static ImGuiNET.ImGui;

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

        public void Enable()
        {
            if (SceneManager.CurrentScene == null!)
            {
                Log.Error("Editor can not be opened if no scene is loaded.");
                return;
            }
            Enabled = true;

            Log.Information($"Starting Scene Editor for \"{SceneManager.CurrentScene.Name}\"...");

            _scene = SceneManager.ParseScene(SceneManager.CurrentScene.Name!/*костыль*/); // TODO: set json path to scene's property
            SceneManager.LoadScene(_scene, true);

            IsVisible = true;
        }

        public void Disable()
        {
            Enabled = false;
            IsVisible = false;
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

            // Define the width for the sidebar (Hierarchy + Inspector)
            float sidebarWidth = 300;

            // Calculate positions and sizes for Scene Hierarchy
            Vector2 hierarchyWindowPos = new Vector2(viewport.WorkPos.X + totalWorkWidth - sidebarWidth, viewport.WorkPos.Y);
            // Hierarchy takes the top half of the sidebar
            Vector2 hierarchyWindowSize = new Vector2(sidebarWidth, totalWorkHeight / 2);

            SetNextWindowPos(hierarchyWindowPos, ImGuiCond.Always);
            SetNextWindowSize(hierarchyWindowSize, ImGuiCond.Always);

            ImGuiWindowFlags flags = ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse;

            Begin("Scene Hierarchy", flags);
            //add button
            Separator();
            foreach (var obj in SceneManager.CurrentScene.GameObjects.Where(s => s.Parent == null))
            {
                DrawObjectTree(obj);
            }
            End();

            // Calculate positions and sizes for Inspector
            Vector2 inspectorWindowPos = new Vector2(viewport.WorkPos.X + totalWorkWidth - sidebarWidth, viewport.WorkPos.Y + totalWorkHeight / 2);
            // Inspector takes the bottom half of the sidebar
            Vector2 inspectorWindowSize = new Vector2(sidebarWidth, totalWorkHeight / 2);

            SetNextWindowPos(inspectorWindowPos, ImGuiCond.Always);
            SetNextWindowSize(inspectorWindowSize, ImGuiCond.Always);

            DrawInspector(); // Call DrawInspector, which now uses the calculated position and size
        }

        private void DrawInspector()
        {
            Begin($"Inspector", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoResize);

            if (_selectedObject == null)
            {
                End();
                return;
            }

            if (BeginTable("InspectorTable", 2, ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
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
                var name = _selectedObject.Name ?? "";
                TableSetColumnIndex(1);
                InputText("##name", ref name, 255);

                EndTable();
            }
            End();
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

    }
}
