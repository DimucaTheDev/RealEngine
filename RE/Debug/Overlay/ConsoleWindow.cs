using ImGuiNET;
using OpenTK.Windowing.Common;
using RE.Core;
using RE.Rendering;
using Serilog;
using System.Numerics;

namespace RE.Debug.Overlay
{
    internal class ConsoleWindow : Renderable
    {
        public static ConsoleWindow? Instance = null!;

        public override RenderLayer RenderLayer => RenderLayer.ImGui;
        public override bool IsVisible { get; set; } = false;

        private ConsoleWindow()
        {
            RenderManager.AddRenderable(this);
        }

        private static string _inputBuffer = string.Empty;
        private static Vector2 _consoleSize = new(600, 300);
        private static Vector2 _consolePos = new(20, 20);
        private static bool _scrollToBottom = false;


        public override void Render(FrameEventArgs args)
        {

            ImGui.SetNextWindowSize(_consoleSize, ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(_consolePos, ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowBgAlpha(.5f);
            if (ImGui.Begin("Console", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysVerticalScrollbar | ImGuiWindowFlags.NoDocking))
            {
                ImGui.BeginChild("ScrollRegion", new Vector2(0, -ImGui.GetFrameHeightWithSpacing()), ImGuiChildFlags.Border, ImGuiWindowFlags.HorizontalScrollbar);

                ImGui.TextUnformatted(Game.GameLog.ToString());

                if (_scrollToBottom)
                {
                    ImGui.SetScrollHereY(1.0f);
                    _scrollToBottom = false;
                }
                ImGui.EndChild();

                ImGui.PushItemWidth(-1);
                if (ImGui.InputText("##ConsoleInput", ref _inputBuffer, 512, ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    if (!string.IsNullOrWhiteSpace(_inputBuffer))
                    {
                        Log.Information("CONSOLE: " + _inputBuffer);
                        _inputBuffer = string.Empty;
                        ImGui.SetKeyboardFocusHere();
                    }
                }
                ImGui.PopItemWidth();
            }
            ImGui.End();
            ImGui.PopStyleColor();
        }
        public static void Init()
        {
            Instance ??= new ConsoleWindow();
        }
    }
}
