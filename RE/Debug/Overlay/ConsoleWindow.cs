using System.Numerics;
using System.Text.RegularExpressions;
using Hexa.NET.ImGui;
using OpenTK.Windowing.Common;
using RE.Core;
using RE.Core.Scripting;
using RE.Editor;
using RE.Rendering;
using Serilog;

namespace RE.Debug.Overlay
{
    public class ConsoleWindow : Renderable
    {
        public static ConsoleWindow? Instance = null!;

        public override RenderLayer RenderLayer => RenderLayer.ImGui;
        public override bool IsVisible { get; set; } = false;

        private static bool _shouldScrollToBottom = true;
        private static string _inputBuffer = string.Empty;
        private static readonly Vector2 _consoleSize = new(600, 300);
        private static readonly Vector2 _consolePos = new(20, 20);
        private static readonly Vector4 _colorDefault = new(0.5f, 0.5f, 0.5f, 1.0f);
        private static readonly Vector4 _colorInfo = new(0.75f, 0.75f, 0.75f, 1.0f);
        private static readonly Vector4 _colorWarning = new(1.0f, 1.0f, 0.0f, 1.0f);
        private static readonly Vector4 _colorError = new(1.0f, 0.4f, 0.4f, 1.0f);

        private bool _focusNextFrame = false;
        private bool _showInfo = true;
        private bool _showWarn = true;
        private bool _showError = true;

        public required string Id;

        public override void Render(FrameEventArgs args)
        {
            ImGui.SetNextWindowSize(_consoleSize, ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(_consolePos, ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowBgAlpha(.80f);

            var size = SceneEditor.Enabled ? ImGui.GetContentRegionAvail() : new Vector2(800, 400);
            ImGui.SetNextWindowSize(size, ImGuiCond.Appearing);
            if (ImGui.Begin("Console ##" + Id))
            {
                ImGui.Checkbox($"Info ({Regex.Matches(GameLogger.Log, "INF]", RegexOptions.Compiled).Count})", ref _showInfo);
                ImGui.SameLine();
                ImGui.Checkbox($"Warning ({Regex.Matches(GameLogger.Log, "WRN]", RegexOptions.Compiled).Count})", ref _showWarn);
                ImGui.SameLine();
                ImGui.Checkbox($"Error ({Regex.Matches(GameLogger.Log, "ERR]", RegexOptions.Compiled).Count})", ref _showError);

                ImGui.Separator();

                float footerHeightToReserve = ImGui.GetFrameHeightWithSpacing();

                var w = (bool)Variables.GetVariable("wrapConsole")!;
                string[] logLines = GameLogger.Log.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

                if (ImGui.BeginChild("ScrollRegion",
                        new Vector2(0, -footerHeightToReserve),
                        ImGuiChildFlags.Borders,
                        w ? ImGuiWindowFlags.None : ImGuiWindowFlags.HorizontalScrollbar))
                {
                    float scrollMaxY = ImGui.GetScrollMaxY();
                    float scrollY = ImGui.GetScrollY();

                    if (scrollY < scrollMaxY)
                        _shouldScrollToBottom = false;
                    if (scrollY >= scrollMaxY)
                        _shouldScrollToBottom = true;

                    ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4, 1));
                    foreach (string line in logLines)
                    {
                        if (line.Contains("INF] ") && !_showInfo)
                            continue;
                        if (line.Contains("WRN] ") && !_showWarn)
                            continue;
                        if (line.Contains("ERR] ") && !_showError)
                            continue;
                        DrawLogLine(line, w);
                    }
                    ImGui.PopStyleVar();

                    if (_shouldScrollToBottom)
                    {
                        ImGui.SetScrollHereY(1.0f);
                    }
                }
                ImGui.EndChild();

                ImGui.PushItemWidth(-1);
                if (_focusNextFrame)
                {
                    ImGui.SetKeyboardFocusHere();
                    _focusNextFrame = false;
                }

                if (ImGui.InputText("##ConsoleInput", ref _inputBuffer, 512, ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    if (!string.IsNullOrWhiteSpace(_inputBuffer))
                    {
                        Log.Information(">>> {Input}", _inputBuffer);
                        CommandHandler.ExecuteCommand(_inputBuffer);
                        _inputBuffer = string.Empty;
                        _shouldScrollToBottom = true;
                        _focusNextFrame = true;
                    }
                }
                ImGui.PopItemWidth();
            }
            ImGui.End(); 
        }
        private void DrawLogLine(string logLine, bool wrap)
        {
            Vector4 color;

            if (logLine.Contains(" INF] "))
            {
                color = _colorInfo;
            }
            else if (logLine.Contains(" WRN] "))
            {
                color = _colorWarning;
            }
            else if (logLine.Contains(" ERR] "))
            {
                color = _colorError;
            }
            else
            {
                color = _colorDefault;
            }

            ImGui.PushStyleColor(ImGuiCol.Text, color);

            if (wrap)
                ImGui.TextWrapped(logLine);
            else
                ImGui.TextUnformatted(logLine);

            ImGui.PopStyleColor();
        }
        public static void Init()
        {
            Instance ??= new ConsoleWindow() { Id = "Main" };
            RenderManager.AddRenderable(Instance);
        }
    }
}
