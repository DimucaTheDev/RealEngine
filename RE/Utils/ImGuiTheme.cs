using System.Numerics;
using ImGuiNET;

namespace RE.Utils
{
    public class ImGuiTheme
    {
        public static void ApplyDarkTheme()
        {
            ImGuiStylePtr style = ImGui.GetStyle();
            var io = ImGui.GetIO(); // For IO-related settings like HoverFlags

            // --- Colors (from previous conversion) ---
            style.Colors[(int)ImGuiCol.Text] = new Vector4(1.00f, 1.00f, 1.00f, 1.00f);
            style.Colors[(int)ImGuiCol.TextDisabled] = new Vector4(0.50f, 0.50f, 0.50f, 1.00f);
            style.Colors[(int)ImGuiCol.WindowBg] = new Vector4(0.06f, 0.06f, 0.06f, 0.94f);
            style.Colors[(int)ImGuiCol.ChildBg] = new Vector4(0.00f, 0.00f, 0.00f, 0.00f);
            style.Colors[(int)ImGuiCol.PopupBg] = new Vector4(0.08f, 0.08f, 0.08f, 0.94f);
            style.Colors[(int)ImGuiCol.Border] = new Vector4(0.43f, 0.43f, 0.50f, 0.50f);
            style.Colors[(int)ImGuiCol.BorderShadow] = new Vector4(0.00f, 0.00f, 0.00f, 0.00f);
            style.Colors[(int)ImGuiCol.FrameBg] = new Vector4(0.00f, 0.00f, 0.00f, 0.65f);
            style.Colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.00f, 0.00f, 0.00f, 0.40f);
            style.Colors[(int)ImGuiCol.FrameBgActive] = new Vector4(0.14f, 0.14f, 0.14f, 1.00f);
            style.Colors[(int)ImGuiCol.TitleBg] = new Vector4(0.04f, 0.04f, 0.04f, 1.00f);
            style.Colors[(int)ImGuiCol.TitleBgActive] = new Vector4(0.00f, 0.00f, 0.00f, 1.00f);
            style.Colors[(int)ImGuiCol.TitleBgCollapsed] = new Vector4(0.00f, 0.00f, 0.00f, 0.51f);
            style.Colors[(int)ImGuiCol.MenuBarBg] = new Vector4(0.14f, 0.14f, 0.14f, 1.00f);
            style.Colors[(int)ImGuiCol.ScrollbarBg] = new Vector4(0.02f, 0.02f, 0.02f, 0.53f);
            style.Colors[(int)ImGuiCol.ScrollbarGrab] = new Vector4(0.31f, 0.31f, 0.31f, 1.00f);
            style.Colors[(int)ImGuiCol.ScrollbarGrabHovered] = new Vector4(0.41f, 0.41f, 0.41f, 1.00f);
            style.Colors[(int)ImGuiCol.ScrollbarGrabActive] = new Vector4(0.51f, 0.51f, 0.51f, 1.00f);
            style.Colors[(int)ImGuiCol.CheckMark] = new Vector4(1.00f, 1.00f, 1.00f, 1.00f);
            style.Colors[(int)ImGuiCol.SliderGrab] = new Vector4(0.48f, 0.48f, 0.48f, 1.00f);
            style.Colors[(int)ImGuiCol.SliderGrabActive] = new Vector4(0.48f, 0.48f, 0.48f, 1.00f);
            style.Colors[(int)ImGuiCol.Button] = new Vector4(0.00f, 0.00f, 0.00f, 1.00f);
            style.Colors[(int)ImGuiCol.ButtonHovered] = new Vector4(0.42f, 0.42f, 0.42f, 1.00f);
            style.Colors[(int)ImGuiCol.ButtonActive] = new Vector4(0.54f, 0.54f, 0.54f, 1.00f);
            style.Colors[(int)ImGuiCol.Header] = new Vector4(0.26f, 0.59f, 0.98f, 0.31f);
            style.Colors[(int)ImGuiCol.HeaderHovered] = new Vector4(0.26f, 0.59f, 0.98f, 0.80f);
            style.Colors[(int)ImGuiCol.HeaderActive] = new Vector4(0.26f, 0.59f, 0.98f, 1.00f);
            style.Colors[(int)ImGuiCol.Separator] = new Vector4(1.00f, 1.00f, 1.00f, 0.50f);
            style.Colors[(int)ImGuiCol.SeparatorHovered] = new Vector4(1.00f, 1.00f, 1.00f, 0.78f);
            style.Colors[(int)ImGuiCol.SeparatorActive] = new Vector4(1.00f, 1.00f, 1.00f, 1.00f);
            style.Colors[(int)ImGuiCol.ResizeGrip] = new Vector4(1.00f, 1.00f, 1.00f, 0.22f);
            style.Colors[(int)ImGuiCol.ResizeGripHovered] = new Vector4(1.00f, 1.00f, 1.00f, 0.67f);
            style.Colors[(int)ImGuiCol.ResizeGripActive] = new Vector4(1.00f, 1.00f, 1.00f, 0.95f);
            style.Colors[(int)ImGuiCol.Tab] = new Vector4(0.30f, 0.30f, 0.30f, 0.86f);
            style.Colors[(int)ImGuiCol.TabHovered] = new Vector4(0.52f, 0.52f, 0.52f, 0.80f);
            style.Colors[(int)ImGuiCol.TabActive] = new Vector4(0.55f, 0.55f, 0.55f, 1.00f);
            style.Colors[(int)ImGuiCol.TabUnfocused] = new Vector4(0.45f, 0.45f, 0.45f, 0.97f);
            style.Colors[(int)ImGuiCol.TabUnfocusedActive] = new Vector4(0.39f, 0.39f, 0.39f, 1.00f);
            style.Colors[(int)ImGuiCol.DockingPreview] = new Vector4(0.00f, 0.00f, 0.00f, 0.70f);
            style.Colors[(int)ImGuiCol.DockingEmptyBg] = new Vector4(0.20f, 0.20f, 0.20f, 1.00f);
            style.Colors[(int)ImGuiCol.PlotLines] = new Vector4(0.61f, 0.61f, 0.61f, 1.00f);
            style.Colors[(int)ImGuiCol.PlotLinesHovered] = new Vector4(1.00f, 0.43f, 0.35f, 1.00f);
            style.Colors[(int)ImGuiCol.PlotHistogram] = new Vector4(0.90f, 0.70f, 0.00f, 1.00f);
            style.Colors[(int)ImGuiCol.PlotHistogramHovered] = new Vector4(1.00f, 0.60f, 0.00f, 1.00f);
            style.Colors[(int)ImGuiCol.TableHeaderBg] = new Vector4(0.19f, 0.19f, 0.20f, 1.00f);
            style.Colors[(int)ImGuiCol.TableBorderStrong] = new Vector4(0.31f, 0.31f, 0.35f, 1.00f);
            style.Colors[(int)ImGuiCol.TableBorderLight] = new Vector4(0.23f, 0.23f, 0.25f, 1.00f);
            style.Colors[(int)ImGuiCol.TableRowBg] = new Vector4(0.00f, 0.00f, 0.00f, 0.00f);
            style.Colors[(int)ImGuiCol.TableRowBgAlt] = new Vector4(1.00f, 1.00f, 1.00f, 0.06f);
            style.Colors[(int)ImGuiCol.TextSelectedBg] = new Vector4(0.48f, 0.48f, 0.48f, 0.48f);
            style.Colors[(int)ImGuiCol.DragDropTarget] = new Vector4(1.00f, 1.00f, 0.00f, 0.90f);
            style.Colors[(int)ImGuiCol.NavHighlight] = new Vector4(0.26f, 0.59f, 0.98f, 1.00f);
            style.Colors[(int)ImGuiCol.NavWindowingHighlight] = new Vector4(1.00f, 1.00f, 1.00f, 0.70f);
            style.Colors[(int)ImGuiCol.NavWindowingDimBg] = new Vector4(0.80f, 0.80f, 0.80f, 0.20f);
            style.Colors[(int)ImGuiCol.ModalWindowDimBg] = new Vector4(0.80f, 0.80f, 0.80f, 0.35f);

            // --- Main Section ---
            style.WindowPadding = new Vector2(8.0f, 8.0f);
            style.FramePadding = new Vector2(4.0f, 3.0f);
            style.ItemSpacing = new Vector2(8.0f, 4.0f);
            style.ItemInnerSpacing = new Vector2(4.0f, 4.0f);
            style.TouchExtraPadding = new Vector2(0.0f, 0.0f);
            style.IndentSpacing = 21.0f;
            style.ScrollbarSize = 14.0f;
            style.GrabMinSize = 12.0f;

            // --- Borders Section ---
            style.WindowBorderSize = 0.0f;
            style.ChildBorderSize = 1.0f;
            style.PopupBorderSize = 0.0f;
            style.FrameBorderSize = 1.0f;
            style.TabBorderSize = 1.0f;
            style.TabBarBorderSize = 0.0f;

            // --- Rounding Section ---
            style.WindowRounding = 6.0f;
            style.ChildRounding = 6.0f;
            style.FrameRounding = 6.0f;
            style.PopupRounding = 0.0f;
            style.ScrollbarRounding = 9.0f;
            style.GrabRounding = 3.0f;
            style.TabRounding = 5.0f;

            // --- Tables Section ---
            style.CellPadding = new Vector2(2.0f, 3.0f);
            style.TableAngledHeadersAngle = 35.0f; // This looks like it might be a custom or less common style property, but ImGui.NET has it.

            // --- Widgets Section (from previous conversion, just keeping for completeness) ---
            style.WindowMenuButtonPosition = ImGuiDir.None;
            style.ColorButtonPosition = ImGuiDir.Left;
            style.ButtonTextAlign = new Vector2(1.00f, 0.00f);
            style.SelectableTextAlign = new Vector2(1.00f, 0.00f);
            style.SeparatorTextBorderSize = 3.0f;
            style.SeparatorTextAlign = new Vector2(0.51f, 0.51f);
            style.SeparatorTextPadding = new Vector2(0.0f, 0.0f);
            style.LogSliderDeadzone = 7.0f;
            style.WindowTitleAlign = new(0.5f);

            // --- Docking section (from previous conversion) ---
            style.DockingSeparatorSize = 0.0f;


            // --- Misc section (from previous conversion) ---
            style.DisplaySafeAreaPadding = new Vector2(3.0f, 3.0f);
        }
    }
}
