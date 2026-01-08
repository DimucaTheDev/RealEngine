using System;
using System.Collections.Generic;
using System.Text;
using Hexa.NET.ImGui;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using RE.Editor;
using RE.Utils;
using TkMouseButton = OpenTK.Windowing.GraphicsLibraryFramework.MouseButton;

namespace RE.Core.Input
{
    public static class Mouse
    {
        public static bool IsInputEnabled { get; set; } = true;
        public static CursorState CursorState { get => Game.Instance.CursorState; set => Game.Instance.CursorState = value; }

        public static bool CanCaptureInput => IsInputEnabled && !ImGui.GetIO().WantCaptureMouse;
        public static Vector2 ScreenPosition => ImGui.GetMousePos().ToOpenTkVector2();
        public static Vector2 LocalPosition => TkState.Position;
        public static Vector2 Delta => ImGui.GetIO().MouseDelta.ToOpenTkVector2();
        public static float ScrollDelta => ImGui.GetIO().MouseWheel;

        private static MouseState TkState => Game.Instance.MouseState;

        public static bool IsButtonDown(TkMouseButton button, bool force = false)
        {
            return (CanCaptureInput || force) && ((!SceneEditor.Enabled && TkState.IsButtonDown(button)) || (SceneEditor.Enabled && ImGui.IsMouseDown((ImGuiMouseButton)button)));
        }

        public static bool IsButtonPressed(TkMouseButton button, bool force = false)
        {
            return (CanCaptureInput || force) && ((!SceneEditor.Enabled && TkState.IsButtonPressed(button)) || (SceneEditor.Enabled && ImGui.IsMouseClicked((ImGuiMouseButton)button)));
        }

        public static bool IsButtonReleased(TkMouseButton button, bool force = false)
        {
            return (CanCaptureInput || force) && ((!SceneEditor.Enabled && TkState.IsButtonReleased(button)) || (SceneEditor.Enabled && ImGui.IsMouseReleased((ImGuiMouseButton)button)));
        }

        public static unsafe void SetLocalPosition(System.Numerics.Vector2 pos)
        {
            GLFW.SetCursorPos((Window*)ImGui.GetWindowViewport().PlatformHandle, pos.X, pos.Y); 
        }
    }
}
