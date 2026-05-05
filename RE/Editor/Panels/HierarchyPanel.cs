using System.Numerics;
using Hexa.NET.ImGui;
using RE.Core.World;
using static Hexa.NET.ImGui.ImGui;

namespace RE.Editor.Panels
{
    internal class HierarchyPanel
    {
        public void Draw()
        {
            ImGuiViewportPtr viewport = GetMainViewport();
            float sidebarWidth = 400;

            SetNextWindowPos(new Vector2(8, 27), ImGuiCond.FirstUseEver);
            SetNextWindowSize(new Vector2(310, 665), ImGuiCond.FirstUseEver);


            ImGuiWindowFlags flags = /*ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse |*/ ImGuiWindowFlags.MenuBar;

            bool renderAbout = false;
            bool renderQuit = false;

            Begin("Scene Hierarchy", flags);

            var gameObjects = SceneManager.CurrentScene.GameObjects;
            foreach (var obj in gameObjects.Where(s => s is { DoNotShowInEditor: false, Parent: null }).ToList())
            {
                DrawObjectTree(obj);
            }

            if (BeginPopupContextWindow())
            {
                if (MenuItem("Create object"))
                {
                    gameObjects.Add(new GameObject { Name = $"New Object {gameObjects.Count() + 1}" });
                }
                EndPopup();
            }

            End();

        }
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
                    //   _scene.GameObjects.Remove(obj!);
                    //   _selectedObject = null;
                    //   UpdateSelection();
                }
                //if (IsItemHovered())
                //   _popupOpened = true;
                //if (_popupOpened && !IsItemHovered())
                // {
                //     CloseCurrentPopup();
                //     _popupOpened = false;
                // }
                EndPopup();
            }
            PopStyleColor();

            if (IsItemClicked())
            {
                SceneEditor.SelectedObject = obj;
            }

            if (hasVisibleChildren && nodeOpen)
            {
                foreach (var child in obj.Children.Where(s => !s.DoNotShowInEditor))
                    DrawObjectTree(child);

                TreePop();
            }
        }
        /*  public void DrawQuit()
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

                  float textHeight = GetTextLineHeightWithSpacing();
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
          }*/
    }
}
