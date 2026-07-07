using System.Runtime.InteropServices;
using Hexa.NET.ImGui;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using static Hexa.NET.ImGui.ImGui;
using RE.Core;
using RE.Core.Assets;
using RE.Core.Input;
using RE.Rendering;
using RE.Rendering.Text;
using RE.Utils;
using Serilog;
using ImFontConfig = Hexa.NET.ImGui.ImFontConfig;
using Vector2 = System.Numerics.Vector2;

namespace RE.Editor.Panels;

public class GamePanel
{
    public Vector2 ViewportSize { get; set; }

    private ImFontPtr font;
    private bool wasOnTitleBar = false;

    public void Draw()
    {
        RenderManager.RenderTo(Time.DeltaTime, Camera.Main);

        Begin($"Game Window {(SceneEditor.SimulationRunning ? "" : "(Paused)")} ###game_panel",
            (SceneEditor.GamePanelActive || !wasOnTitleBar) ? ImGuiWindowFlags.NoMove : ImGuiWindowFlags.None);
        var contentSize = GetContentRegionAvail();

        if (wasOnTitleBar = IsInTitleBar())
        {
            Image(Camera.Main.RenderTexture!, contentSize, new(0, 1), new(1, 0));
            End();
            return;
        }

        if (contentSize is { X: > 0, Y: > 0 } &&
            (ViewportSize.X != contentSize.X || ViewportSize.Y != contentSize.Y))
        {
            ViewportSize = contentSize;
            Camera.Main.ResizeFramebuffers((int)ViewportSize.X, (int)ViewportSize.Y);
            Camera.Main.RenderWidth = (int)ViewportSize.X;
            Camera.Main.RenderHeight = (int)ViewportSize.Y;
        }

        Image(Camera.Main.RenderTexture!, contentSize, new(0, 1), new(1, 0));


        if (SceneEditor.SimulationRunning && IsItemClicked())
        {
            SceneEditor.GamePanelActive = true;
            Mouse.CursorState = CursorState.Grabbed;
            CursorLocker.LockToImGuiWindow();
            End();
            return;
        }

        if (SceneEditor.GamePanelActive && Mouse.CursorState == CursorState.Grabbed)
            CursorLocker.Update(true);

        if (SceneEditor.GamePanelActive && Keyboard.IsKeyDown(Keys.Escape))
        {
            SceneEditor.GamePanelActive = false;
            Mouse.CursorState = CursorState.Normal;
            CursorLocker.Unlock();
        }

        End();
    }

    bool IsInTitleBar()
    {
        Vector2 mousePos = ImGui.GetMousePos();
        Vector2 windowPos = ImGui.GetWindowPos();
        Vector2 windowSize = ImGui.GetWindowSize();

        float titleBarHeight = ImGui.GetFontSize() + ImGui.GetStyle().FramePadding.Y * 2.0f;


        var r = (mousePos.X >= windowPos.X && mousePos.X <= windowPos.X + windowSize.X &&
                 mousePos.Y >= windowPos.Y && mousePos.Y <= windowPos.Y + titleBarHeight);
        return r;
    }
}

public static class CursorLocker
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern int ShowCursor(bool bShow);

    [DllImport("user32.dll")]
    private static extern bool ClipCursor(ref RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool ClipCursor(IntPtr lpRect);

    public static void LockToImGuiWindow()
    {
        Vector2 pos = ImGui.GetWindowPos();
        Vector2 size = ImGui.GetWindowSize();

        RECT rect = new RECT
        {
            Left = (int)pos.X,
            Top = (int)pos.Y,
            Right = (int)(pos.X + size.X),
            Bottom = (int)(pos.Y + size.Y)
        };

        ClipCursor(ref rect);
    }

    public static void Unlock()
    {
        ClipCursor(IntPtr.Zero);
        while (ShowCursor(true) < 0)
        {
        }

        SetMouseCursor(ImGuiMouseCursor.Arrow);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int X, int Y);


    public static void Update(bool isWindowFocused)
    {
        Vector2 wPos = ImGui.GetWindowPos();
        Vector2 wSize = ImGui.GetWindowSize();
        int centerX = (int)(wPos.X + wSize.X / 2);
        int centerY = (int)(wPos.Y + wSize.Y / 2);

        if (GetCursorPos(out POINT currentPos))
        {
            int deltaX = currentPos.X - centerX;
            int deltaY = currentPos.Y - centerY;

            if (deltaX != 0 || deltaY != 0)
            {
                if (GetWindowViewport() != GetMainViewport())
                {
                    SetCursorPos(centerX, centerY);
                    GetIO().MouseDelta = new Vector2(deltaX, deltaY);
                }

                GetIO().MouseDrawCursor = false;
                SetMouseCursor(ImGuiMouseCursor.None);

                while (ShowCursor(false) >= 0)
                {
                }
            }
        }
    }
}