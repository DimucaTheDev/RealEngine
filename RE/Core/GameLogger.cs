using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace RE.Core
{
    internal class GameLogger(string outputTemplate) : ILogEventSink
    {
        private readonly MessageTemplateTextFormatter _formatter = new(outputTemplate, null);
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
