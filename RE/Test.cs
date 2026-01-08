using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Backends.GLFW;
using Hexa.NET.ImGui.Backends.OpenGL3;
using Hexa.NET.OpenGL;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace RE
{
    internal class Test : GameWindow
    {
        /// <inheritdoc />
        public Test(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings) : base(gameWindowSettings, nativeWindowSettings) { }

        /// <inheritdoc />
        protected override unsafe void OnLoad()
        {
            base.OnLoad();

            var guiContext = ImGui.CreateContext();
            ImGui.SetCurrentContext(guiContext);

            // Setup ImGui config.
            var io = ImGui.GetIO();
            io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;     // Enable Keyboard Controls
            io.ConfigFlags |= ImGuiConfigFlags.NavEnableGamepad;      // Enable Gamepad Controls
            io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;         // Enable Docking
            io.ConfigFlags |= ImGuiConfigFlags.ViewportsEnable;       // Enable Multi-Viewport / Platform Windows

            ImGui.StyleColorsDark();
            var style = ImGui.GetStyle();

            if ((io.ConfigFlags & ImGuiConfigFlags.ViewportsEnable) != 0)
            {
                style.WindowRounding = 0.0f;
                style.Colors[(int)ImGuiCol.WindowBg].W = 1.0f;
            }

            ImGuiImplGLFW.SetCurrentContext(guiContext);

            if (!ImGuiImplGLFW.InitForOpenGL((GLFWwindow*)WindowPtr, true))
            {
                Console.WriteLine("Failed to init ImGui Impl GLFW");
                return;
            }

            ImGuiImplOpenGL3.SetCurrentContext(guiContext);
            if (!ImGuiImplOpenGL3.Init("#version 150"))
            {
                Console.WriteLine("Failed to init ImGui Impl OpenGL3");
                return;
            }
        }

        /// <inheritdoc />
        protected override void OnRenderFrame(FrameEventArgs args)
        {
            var io = ImGui.GetIO();
            OpenTK.Graphics.OpenGL.GL.ClearColor(1, 0.8f, 0.75f, 1);
            OpenTK.Graphics.OpenGL.GL.Clear(ClearBufferMask.ColorBufferBit);

            ImGuiImplOpenGL3.NewFrame();
            ImGuiImplGLFW.NewFrame();
            ImGui.NewFrame();

            ImGui.ShowDemoWindow();

            ImGui.Render();

            MakeCurrent();
            ImGuiImplOpenGL3.RenderDrawData(ImGui.GetDrawData());

            if ((io.ConfigFlags & ImGuiConfigFlags.ViewportsEnable) != 0)
            {
                ImGui.UpdatePlatformWindows();
                ImGui.RenderPlatformWindowsDefault();
            }

            MakeCurrent();
            SwapBuffers();
        }
    }
} 