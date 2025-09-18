using System.Numerics;
using ImGuiNET;
using OpenTK.Windowing.Common;
using RE.Core;
using RE.Core.Scripting;
using RE.Rendering;
using Serilog;

namespace RE.Debug.Overlay
{
    public class ConsoleWindow : Renderable
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


        private bool _focusNextFrame = false;

        public void Focus() => _focusNextFrame = true;

        public override void Render(FrameEventArgs args)
        {
            ImGui.SetNextWindowSize(_consoleSize, ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(_consolePos, ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowBgAlpha(.80f);

            if (ImGui.Begin("Console", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking))
            {
                var w = (bool)Variables.GetVariable("wrapConsole")!;
                ImGui.BeginChild("ScrollRegion", new Vector2(0, -ImGui.GetFrameHeightWithSpacing()), ImGuiChildFlags.Border, w ? ImGuiWindowFlags.None : ImGuiWindowFlags.HorizontalScrollbar);

                if (w)
                    ImGui.TextWrapped(GameLogger.Log);
                else
                    ImGui.TextUnformatted(GameLogger.Log);

                if (_scrollToBottom)
                {
                    ImGui.SetScrollHereY(9999.0f);
                    _scrollToBottom = false;
                }
                ImGui.EndChild();

                ImGui.PushItemWidth(-1);
                if (_focusNextFrame)
                {
                    ImGui.SetKeyboardFocusHere();
                    _focusNextFrame = false;
                }

                //ImGui.SetKeyboardFocusHere();//fixme: fix focus

                if (ImGui.InputText("##ConsoleInput", ref _inputBuffer, 512, ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    if (!string.IsNullOrWhiteSpace(_inputBuffer))
                    {
                        Log.Information(">>> {Input}", _inputBuffer);
                        CommandHandler.ExecuteCommand(_inputBuffer);
                        _inputBuffer = string.Empty;
                        _scrollToBottom = true;

                        _focusNextFrame = true; // отложим фокус
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
