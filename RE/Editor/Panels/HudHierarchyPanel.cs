using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Hexa.NET.ImGui;
using RE.Core.Ui;
using RE.Core.Ui.Controls;
using RE.Core.World;
using static Hexa.NET.ImGui.ImGui;

namespace RE.Editor.Panels
{
    internal class HudHierarchyPanel
    {
        public void Draw()
        {
            ImGuiViewportPtr viewport = GetMainViewport();  
 
            SetNextWindowPos(new Vector2(8, 27), ImGuiCond.FirstUseEver);
            SetNextWindowSize(new Vector2(310, 665), ImGuiCond.FirstUseEver);
 
            ImGuiWindowFlags flags = /*ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse |*/ ImGuiWindowFlags.MenuBar;
 

            Begin("UI Hierarchy", flags);

            DrawHudControlNode(Hud.Root);

            End(); 
        } 

        private void DrawHudControlNode(UiControl control)
        {
            string label = $"[{control.ZIndex}] {control.Name}";

            var uiControlList = control.Children;
            bool opened = TreeNodeEx(label,
                ImGuiTreeNodeFlags.DefaultOpen | (!uiControlList.Any() ? ImGuiTreeNodeFlags.Leaf : 0));

            if (opened)
            {
                var children = uiControlList;

                foreach (var child in children)
                    DrawHudControlNode(child);

                TreePop();
            }
        }
    }
}
