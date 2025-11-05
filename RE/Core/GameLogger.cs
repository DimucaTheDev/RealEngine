using System.Diagnostics;
using System.Reflection;
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
            if (logEvent.Level >= LogEventLevel.Error)
            {
                /*var e = logEvent.Exception ?? throw new Exception(logEvent.RenderMessage());
                var s = new StackTrace(e, true).ToString();
                Console.WriteLine(s);
                Log += s;*/
            }
        }
    }

    internal class EngineLoggerEnricher(string baseContext) : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            // Если SourceContext уже явно задан — не трогаем
            if (logEvent.Properties.ContainsKey("SourceContext"))
                return;

            var assembly = GetCallingAssembly();
            var asmName = assembly?.GetName().Name ?? baseContext;

            var contextProp = propertyFactory.CreateProperty("SourceContext", asmName);
            logEvent.AddOrUpdateProperty(contextProp);
        }

        private static Assembly? GetCallingAssembly()
        {
            var trace = new StackTrace();
            for (int i = 0; i < trace.FrameCount; i++)
            {
                var method = trace.GetFrame(i)?.GetMethod();
                if (method == null)
                    continue;

                var asm = method.DeclaringType?.Assembly;
                if (asm == null)
                    continue;

                // Пропускаем системные и Serilog-сборки
                var name = asm.GetName().Name;
                if (name != null &&
                    !name.StartsWith("Serilog", StringComparison.OrdinalIgnoreCase) &&
                    !name.StartsWith("System", StringComparison.OrdinalIgnoreCase) &&
                    !name.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase))
                {
                    return asm;
                }
            }
            return null;
        }
    }
}
