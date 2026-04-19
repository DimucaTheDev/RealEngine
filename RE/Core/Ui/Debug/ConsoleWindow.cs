using System.Numerics;
using System.Text;
using Hexa.NET.ImGui;
using OpenTK.Windowing.Common;
using RE.Core.Logging;
using RE.Core.Scripting;
using RE.Editor;
using RE.Rendering;
using RE.Utils;
using Serilog;
using Serilog.Events;

namespace RE.Core.Ui.Debug
{
    public class ConsoleWindow : Renderable
    {
        public static ConsoleWindow? Instance;

        public override RenderLayer RenderLayer => RenderLayer.ImGui;
        public override bool IsVisible { get; set; }

        private static bool _shouldScrollToBottom = true;
        private static string _inputBuffer = string.Empty;
        private static readonly Vector2 _consoleSize = new(600, 300);
        private static readonly Vector2 _consolePos = new(20, 20);
        private static readonly Vector4 _colorDefault = new(0.5f, 0.5f, 0.5f, 1.0f);
        private static readonly Vector4 _colorInfo = new(0.75f, 0.75f, 0.75f, 1.0f);
        private static readonly Vector4 _colorWarning = new(1.0f, 1.0f, 0.0f, 1.0f);
        private static readonly Vector4 _colorError = new(1.0f, 0.4f, 0.4f, 1.0f);
        private static readonly List<string> _commandHistory = new();

        private bool _focusNextFrame;
        private bool _showInfo = true;
        private bool _showWarn = true;
        private bool _showError = true;
        private int _historyIndex = -1;
        private string _savedInput = string.Empty;
        private int _infoCount, _warnCount, _errorCount;

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
                unsafe
                {
                    if (ImGui.Button("X"))
                    {
                        if (this != Instance)
                            this.StopRender();
                        else
                            IsVisible = false;
                    }

                    var log = GameLogger.Log;
                    ImGui.Checkbox($"Info ({log.Count(s => s.Level is LogEventLevel.Information)})", ref _showInfo);
                    ImGui.SameLine();
                    ImGui.Checkbox($"Warning ({log.Count(s => s.Level is LogEventLevel.Warning)})", ref _showWarn);
                    ImGui.SameLine();
                    ImGui.Checkbox($"Error ({log.Count(s => s.Level is LogEventLevel.Error or LogEventLevel.Fatal)})", ref _showError);

                    ImGui.Separator();

                    float footerHeightToReserve = ImGui.GetFrameHeightWithSpacing();

                    var w = (bool)Variables.GetVariableOrDefault("wrapConsole", false)!;

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
                        if (ImGui.BeginTable("##console", 3))
                        {
                            ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.WidthFixed, 10); // минимальная ширина
                            ImGui.TableSetupColumn("##code", ImGuiTableColumnFlags.WidthFixed, 50); // минимальная ширина
                            ImGui.TableSetupColumn("##message", ImGuiTableColumnFlags.WidthStretch);
                            ImGui.TableHeadersRow();

                            foreach (LogEntry entry in log)
                            {
                                if (entry.Level is LogEventLevel.Information && !_showInfo)
                                    continue;
                                if (entry.Level is LogEventLevel.Warning && !_showWarn)
                                    continue;
                                if (entry.Level is LogEventLevel.Error or LogEventLevel.Fatal && !_showError)
                                    continue;

                                DrawLogLine(entry, w);
                            }

                            ImGui.EndTable();
                            ImGui.Dummy(new Vector2(0, 0)); // фикс для предупреждения
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

                    if (ImGui.InputText("##ConsoleInput", ref _inputBuffer, 512,
                            ImGuiInputTextFlags.EnterReturnsTrue
                            | ImGuiInputTextFlags.CallbackCompletion
                            | ImGuiInputTextFlags.CallbackHistory, ConsoleCallback))
                    {
                        if (!string.IsNullOrWhiteSpace(_inputBuffer))
                        {
                            Log.Information(">>> {Input}", _inputBuffer);
                            CommandHandler.ExecuteCommand(_inputBuffer);
                            if (_commandHistory.LastOrDefault() != _inputBuffer)
                                _commandHistory.Add(_inputBuffer);

                            _historyIndex = -1;
                            _savedInput = string.Empty;
                            _inputBuffer = string.Empty;

                            _focusNextFrame = true;
                        }
                    }
                    ImGui.PopItemWidth();
                }
            }
            ImGui.End();
        }
        private unsafe int ConsoleCallback(ImGuiInputTextCallbackData* data)
        {
            switch (data->EventFlag)
            {
                case ImGuiInputTextFlags.CallbackCompletion:
                    HandleAutocomplete(data);
                    break;

                case ImGuiInputTextFlags.CallbackHistory:
                    if (data->EventKey == ImGuiKey.UpArrow)
                        MoveHistory(1, data);
                    else if (data->EventKey == ImGuiKey.DownArrow)
                        MoveHistory(-1, data);
                    break;
            }
            return 0;
        }

        private unsafe void HandleAutocomplete(ImGuiInputTextCallbackData* data)
        {
            string input = Encoding.UTF8.GetString(data->Buf, data->BufTextLen);

            var matches = CommandHandler.RegisteredCommands
                .Where(c => c.StartsWith(input, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count == 0)
            {
                Log.Information("No matches for input: {Input}", input);

            }
            else if (matches.Count == 1)
            {
                string match = matches[0];
                data->DeleteChars(0, data->BufTextLen);
                data->InsertChars(0, match);
            }
            else
            {
                Log.Information("Matches({Input}): {Matches}", input, string.Join(", ", matches));

                string commonPrefix = FindCommonPrefix(matches);
                if (commonPrefix.Length > input.Length)
                {
                    data->DeleteChars(0, data->BufTextLen);
                    data->InsertChars(0, commonPrefix);
                }
            }
        }

        private string FindCommonPrefix(List<string> strings)
        {
            if (strings.Count == 0)
                return string.Empty;
            string prefix = strings[0];
            for (int i = 1; i < strings.Count; i++)
            {
                while (!strings[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    prefix = prefix.Substring(0, prefix.Length - 1);
                    if (string.IsNullOrEmpty(prefix))
                        return string.Empty;
                }
            }
            return prefix;
        }
        private unsafe void MoveHistory(int direction, ImGuiInputTextCallbackData* data)
        {
            if (_commandHistory.Count == 0)
                return;

            if (_historyIndex == -1)
                _savedInput = _inputBuffer;

            int newIndex = _historyIndex + direction;

            if (newIndex >= -1 && newIndex < _commandHistory.Count)
            {
                _historyIndex = newIndex;
                string newText = (_historyIndex == -1)
                    ? _savedInput
                    : _commandHistory[_commandHistory.Count - 1 - _historyIndex];

                data->DeleteChars(0, data->BufTextLen);
                data->InsertChars(0, newText);
            }
        }
        private void DrawLogLine(LogEntry entry, bool wrap)
        {
            Vector4 color;
            string icon;
            string level;

            switch (entry.Level)
            {
                case LogEventLevel.Information:
                    color = _colorInfo;
                    icon = IconFont.InfoCircle;
                    level = "INFO";
                    break;

                case LogEventLevel.Warning:
                    color = _colorWarning;
                    icon = IconFont.ExclamationTriangle;
                    level = "WARN";
                    break;

                case LogEventLevel.Error:
                case LogEventLevel.Fatal:
                    color = _colorError;
                    icon = IconFont.CrossCircle;
                    level = "ERROR";
                    break;

                default:
                    color = _colorDefault;
                    icon = "  ";
                    level = "LOG";
                    break;
            }

            ImGui.PushID(entry.GetHashCode());

            // Вставляем новую строку таблицы
            ImGui.TableNextRow();

            // Иконка
            ImGui.TableSetColumnIndex(0);
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            ImGui.TextUnformatted(icon);
            ImGui.PopStyleColor();

            // Код/уровень
            ImGui.TableSetColumnIndex(1);
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            ImGui.TextUnformatted(level);
            ImGui.PopStyleColor();

            // Сообщение
            ImGui.TableSetColumnIndex(2);
            ImGui.PushStyleColor(ImGuiCol.Text, _colorDefault);
            if (wrap)
                ImGui.TextWrapped(entry.Message);
            else
                ImGui.TextUnformatted(entry.Message);
            ImGui.PopStyleColor();

            ImGui.PopID();
        }
        public static void Init()
        {
            Instance ??= new ConsoleWindow { Id = "Main" };
            Instance.StartRender();
        }
    }
}
