using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace RE.Core
{
    /// <summary>
    /// This class is used to log game events to a string (<see cref="Log"/>).
    /// </summary>
    /// <example>
    /// Game console (see <see cref="RE.Debug.Overlay.ConsoleWindow"/>) uses this class to display log messages in-game.
    /// </example>
    public class GameLogger(string outputTemplate) : ILogEventSink
    {
        private readonly MessageTemplateTextFormatter _formatter = new(outputTemplate);
        public static string Log = string.Empty;

        public void Emit(LogEvent logEvent)
        {
            if (logEvent.Level < LogEventLevel.Information)
                return;
            using var writer = new StringWriter();
            _formatter.Format(logEvent, writer);
            Log += writer.ToString();
        }
    }
}
