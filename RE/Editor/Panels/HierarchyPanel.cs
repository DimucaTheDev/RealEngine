using System.Diagnostics.Tracing;
using System.Numerics;
using Hexa.NET.ImGui;
using RE.Core.World;
using RE.Core.World.Components;
using RE.Core.World.Components.Physics;
using static Hexa.NET.ImGui.ImGui;

namespace RE.Editor.Panels
{
    internal class HierarchyPanel
    {
        private const string DragDropType = "GAME_OBJECT_HIERARCHY";
        private (GameObject Child, GameObject NewParent)? _reparentRequest = null;

        public void Draw()
        {  
            SetNextWindowPos(new Vector2(8, 27), ImGuiCond.FirstUseEver);
            SetNextWindowSize(new Vector2(310, 665), ImGuiCond.FirstUseEver);
 
            ImGuiWindowFlags flags = ImGuiWindowFlags.MenuBar; 

            Begin("Scene Hierarchy", flags);

            var gameObjects = SceneManager.CurrentScene.GameObjects;
            foreach (var obj in gameObjects.Where(s => s is { DoNotShowInEditor: false, Parent: null }).ToList())
            {
                DrawObjectTree(obj);
            }

            Spacing();
            float remainingY = GetContentRegionAvail().Y;
            if (remainingY > 0)
            {
                Dummy(new Vector2(GetContentRegionAvail().X, remainingY));

                if (BeginDragDropTarget())
                {
                    ImGuiPayloadPtr payload = AcceptDragDropPayload(DragDropType);
                    if (!payload.IsNull)
                    {
                        GameObject draggedObj = DragDropStorage.DraggedObject;

                        if (draggedObj != null && draggedObj.Parent != null)
                        {
                            _reparentRequest = (draggedObj, null);
                        }

                        DragDropStorage.DraggedObject = null;
                    }

                    EndDragDropTarget();
                }
            }

            DrawContextWindow(gameObjects);

            if (_reparentRequest.HasValue)
            {
                PerformReparent(_reparentRequest.Value.Child, _reparentRequest.Value.NewParent);
                _reparentRequest = null;
            }

            End();
        }

        private void DrawContextWindow(GameObjectList gameObjects)
        {
            if (BeginPopupContextWindow())
            {
                if (BeginMenu("Create new"))
                {
                    if (MenuItem("Empty object"))
                        gameObjects.Add(new GameObject { Name = $"New Object {gameObjects.Count() + 1}" });

                    if (MenuItem("Cube"))
                        gameObjects.Add(new GameObject
                        {
                            Name = $"New Object {gameObjects.Count() + 1}",
                            Components =
                            {
                                new MeshComponent("assets/models/cub.fbx"),
                                new RigidBodyComponent(0),
                                new BoxColliderComponent()
                            }
                        }); // todo: make new objects parent the one mouse has clicked on in hierarchy

                    Text("todo: more templates");
                    
                    EndMenu();
                }

                Separator();

                MenuItem("Delete object");
                MenuItem("Clone object");
                
                EndPopup();
            }
        }

        private void DrawObjectTree(GameObject obj)
        {
            bool hasVisibleChildren = obj.Children.Any(s => !s.DoNotShowInEditor);

            ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.OpenOnDoubleClick |
                                       ImGuiTreeNodeFlags.DrawLinesFull;

            if (!hasVisibleChildren)
                flags |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;

            if (SceneEditor.SelectedObject == obj)
                flags |= ImGuiTreeNodeFlags.Selected;

            bool nodeOpen = TreeNodeEx(obj.GetHashCode().ToString(), flags, $"{obj.Name ?? "<unnamed>"}");

            if (IsItemClicked() && !IsItemToggledOpen())
            {
                SceneEditor.SelectedObject = obj;
            }

            if (BeginDragDropSource())
            {
                unsafe
                {
                    SetDragDropPayload(DragDropType, (void*)0, 0);
                }


                Text($"Moving: {obj.Name}");

                DragDropStorage.DraggedObject = obj;

                EndDragDropSource();
            }

            if (BeginDragDropTarget())
            {
                ImGuiPayloadPtr payload = AcceptDragDropPayload(DragDropType);

                if (!payload.IsNull)
                {
                    GameObject draggedObj = DragDropStorage.DraggedObject;

                    if (draggedObj != null && draggedObj != obj && draggedObj.Parent != obj)
                    {
                        if (!IsDescendantOf(obj, draggedObj))
                        {
                            _reparentRequest = (draggedObj, obj);
                        }
                    }

                    DragDropStorage.DraggedObject = null;
                }

                EndDragDropTarget();
            }

            PushStyleColor(ImGuiCol.PopupBg, new Vector4(0.1f, 0.1f, 0.1f, 0.95f));
            if (BeginPopupContextItem())
            {
                if (MenuItem("Delete Object"))
                {
                }

                if (MenuItem("Make Root"))
                {
                    _reparentRequest = (obj, null);
                }

                EndPopup();
            }

            PopStyleColor();

            if (hasVisibleChildren && nodeOpen)
            {
                foreach (var child in obj.Children.Where(s => !s.DoNotShowInEditor))
                    DrawObjectTree(child);

                TreePop();
            }
        }

        private void PerformReparent(GameObject child, GameObject? newParent)
        {
            child.Parent = newParent;
            if (newParent != null)
            {
                ImGui.SetNextItemOpen(true);
            }
        }

        private bool IsDescendantOf(GameObject potentialDescendant, GameObject potentialAncestor)
        {
            GameObject? current = potentialDescendant.Parent;
            while (current != null)
            {
                if (current == potentialAncestor)
                    return true;
                current = current.Parent;
            }

            return false;
        }

        private static class DragDropStorage
        {
            public static GameObject DraggedObject;
        }
    }
}