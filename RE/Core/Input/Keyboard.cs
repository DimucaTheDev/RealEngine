using System;
using System.Collections.Generic;
using System.Text;
using Hexa.NET.ImGui;
using OpenTK.Windowing.GraphicsLibraryFramework;
using RE.Editor;
using TkKeys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

namespace RE.Core.Input
{
    public static class Keyboard
    {
        public static bool IsInputEnabled { get; set; } = true;

        public static bool CanCaptureInput => IsInputEnabled ;//&& !ImGui.GetIO().WantCaptureKeyboard;
        public static bool Shift => ImGui.GetIO().KeyShift || TkState.IsKeyDown(TkKeys.LeftShift) || TkState.IsKeyDown(TkKeys.RightShift);
        public static bool Ctrl => ImGui.GetIO().KeyCtrl || TkState.IsKeyDown(TkKeys.LeftControl) || TkState.IsKeyDown(TkKeys.RightControl);
        public static bool Alt => ImGui.GetIO().KeyAlt || TkState.IsKeyDown(TkKeys.LeftAlt) || TkState.IsKeyDown(TkKeys.RightAlt);

        //opentk keyboard state
        private static KeyboardState TkState => Game.Instance.KeyboardState;

        public static bool IsKeyDown(TkKeys key, bool force = false)
        {
            return (CanCaptureInput || force) && ((!SceneEditor.Enabled && TkState.IsKeyDown(key)) || (SceneEditor.Enabled && ImGui.IsKeyDown(GetImGuiKey(key))));
        }
        public static bool IsKeyPressed(TkKeys key, bool force = false)
        {
            return (CanCaptureInput || force) && ((!SceneEditor.Enabled && TkState.IsKeyPressed(key)) || (SceneEditor.Enabled && ImGui.IsKeyPressed(GetImGuiKey(key), false)));
        }
        public static bool IsKeyReleased(TkKeys key, bool force = false)
        {
            return (CanCaptureInput || force) && ((!SceneEditor.Enabled && TkState.IsKeyReleased(key)) || (SceneEditor.Enabled && ImGui.IsKeyReleased(GetImGuiKey(key))));
        }

        private static ImGuiKey GetImGuiKey(TkKeys key)
        {
            return key switch
            {
                TkKeys.A => ImGuiKey.A,
                TkKeys.B => ImGuiKey.B,
                TkKeys.C => ImGuiKey.C,
                TkKeys.D => ImGuiKey.D,
                TkKeys.E => ImGuiKey.E,
                TkKeys.F => ImGuiKey.F,
                TkKeys.G => ImGuiKey.G,
                TkKeys.H => ImGuiKey.H,
                TkKeys.I => ImGuiKey.I,
                TkKeys.J => ImGuiKey.J,
                TkKeys.K => ImGuiKey.K,
                TkKeys.L => ImGuiKey.L,
                TkKeys.M => ImGuiKey.M,
                TkKeys.N => ImGuiKey.N,
                TkKeys.O => ImGuiKey.O,
                TkKeys.P => ImGuiKey.P,
                TkKeys.Q => ImGuiKey.Q,
                TkKeys.R => ImGuiKey.R,
                TkKeys.S => ImGuiKey.S,
                TkKeys.T => ImGuiKey.T,
                TkKeys.U => ImGuiKey.U,
                TkKeys.V => ImGuiKey.V,
                TkKeys.W => ImGuiKey.W,
                TkKeys.X => ImGuiKey.X,
                TkKeys.Y => ImGuiKey.Y,
                TkKeys.Z => ImGuiKey.Z,

                TkKeys.D0 => ImGuiKey.Key0,
                TkKeys.D1 => ImGuiKey.Key1,
                TkKeys.D2 => ImGuiKey.Key2,
                TkKeys.D3 => ImGuiKey.Key3,
                TkKeys.D4 => ImGuiKey.Key4,
                TkKeys.D5 => ImGuiKey.Key5,
                TkKeys.D6 => ImGuiKey.Key6,
                TkKeys.D7 => ImGuiKey.Key7,
                TkKeys.D8 => ImGuiKey.Key8,
                TkKeys.D9 => ImGuiKey.Key9,

                TkKeys.F1 => ImGuiKey.F1,
                TkKeys.F2 => ImGuiKey.F2,
                TkKeys.F3 => ImGuiKey.F3,
                TkKeys.F4 => ImGuiKey.F4,
                TkKeys.F5 => ImGuiKey.F5,
                TkKeys.F6 => ImGuiKey.F6,
                TkKeys.F7 => ImGuiKey.F7,
                TkKeys.F8 => ImGuiKey.F8,
                TkKeys.F9 => ImGuiKey.F9,
                TkKeys.F10 => ImGuiKey.F10,
                TkKeys.F11 => ImGuiKey.F11,
                TkKeys.F12 => ImGuiKey.F12,

                TkKeys.Up => ImGuiKey.UpArrow,
                TkKeys.Down => ImGuiKey.DownArrow,
                TkKeys.Left => ImGuiKey.LeftArrow,
                TkKeys.Right => ImGuiKey.RightArrow,
                TkKeys.Enter => ImGuiKey.Enter,
                TkKeys.Escape => ImGuiKey.Escape,
                TkKeys.Space => ImGuiKey.Space,
                TkKeys.Tab => ImGuiKey.Tab,
                TkKeys.Backspace => ImGuiKey.Backspace,
                TkKeys.Insert => ImGuiKey.Insert,
                TkKeys.Delete => ImGuiKey.Delete,
                TkKeys.PageUp => ImGuiKey.PageUp,
                TkKeys.PageDown => ImGuiKey.PageDown,
                TkKeys.Home => ImGuiKey.Home,
                TkKeys.End => ImGuiKey.End,
                TkKeys.CapsLock => ImGuiKey.CapsLock,
                TkKeys.ScrollLock => ImGuiKey.ScrollLock,
                TkKeys.PrintScreen => ImGuiKey.PrintScreen,
                TkKeys.Pause => ImGuiKey.Pause,

                TkKeys.LeftShift => ImGuiKey.LeftShift,
                TkKeys.RightShift => ImGuiKey.RightShift,
                TkKeys.LeftControl => ImGuiKey.LeftCtrl,
                TkKeys.RightControl => ImGuiKey.RightCtrl,
                TkKeys.LeftAlt => ImGuiKey.LeftAlt,
                TkKeys.RightAlt => ImGuiKey.RightAlt,
                TkKeys.LeftSuper => ImGuiKey.LeftSuper, // Win/Cmd
                TkKeys.RightSuper => ImGuiKey.RightSuper,

                TkKeys.KeyPad0 => ImGuiKey.Keypad0,
                TkKeys.KeyPad1 => ImGuiKey.Keypad1,
                TkKeys.KeyPad2 => ImGuiKey.Keypad2,
                TkKeys.KeyPad3 => ImGuiKey.Keypad3,
                TkKeys.KeyPad4 => ImGuiKey.Keypad4,
                TkKeys.KeyPad5 => ImGuiKey.Keypad5,
                TkKeys.KeyPad6 => ImGuiKey.Keypad6,
                TkKeys.KeyPad7 => ImGuiKey.Keypad7,
                TkKeys.KeyPad8 => ImGuiKey.Keypad8,
                TkKeys.KeyPad9 => ImGuiKey.Keypad9,
                TkKeys.KeyPadDivide => ImGuiKey.KeypadDivide,
                TkKeys.KeyPadMultiply => ImGuiKey.KeypadMultiply,
                TkKeys.KeyPadSubtract => ImGuiKey.KeypadSubtract,
                TkKeys.KeyPadAdd => ImGuiKey.KeypadAdd,
                TkKeys.KeyPadDecimal => ImGuiKey.KeypadDecimal,
                TkKeys.KeyPadEnter => ImGuiKey.KeypadEnter,

                TkKeys.Semicolon => ImGuiKey.Semicolon,
                TkKeys.Slash => ImGuiKey.Slash,
                TkKeys.Equal => ImGuiKey.Equal,
                TkKeys.Minus => ImGuiKey.Minus,
                TkKeys.LeftBracket => ImGuiKey.LeftBracket,
                TkKeys.RightBracket => ImGuiKey.RightBracket,
                TkKeys.Comma => ImGuiKey.Comma,
                TkKeys.Period => ImGuiKey.Period,
                TkKeys.Apostrophe => ImGuiKey.Apostrophe,
                TkKeys.Backslash => ImGuiKey.Backslash,
                TkKeys.GraveAccent => ImGuiKey.GraveAccent,

                _ => throw new ArgumentOutOfRangeException(nameof(key), key, null)
            };
        }
    }
}