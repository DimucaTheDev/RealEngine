using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using RE.Editor.Notification;
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
        public static StringBuilder Log = new(2048);

        public void Emit(LogEvent logEvent)
        {
            if (logEvent.Level < LogEventLevel.Information)
                return;
            using var writer = new StringWriter();
            _formatter.Format(logEvent, writer);
            Log.Append(writer);
        }
    }

    internal class EngineLoggerEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            if (logEvent.Properties.ContainsKey("SourceContext"))
                return;

            var method = GetCallingMethod();

            string name;
            if (method != null)
            {
                var type = method.DeclaringType!;
                var dllFileName = Path.GetFileNameWithoutExtension(type.Assembly.Location);
                name = $"{dllFileName}:{type.Name}/{method.Name}";
            }
            else
                name = "<Native Call>";
            var contextProp = propertyFactory.CreateProperty("SourceContext", name);
            logEvent.AddOrUpdateProperty(contextProp);
        }

        private static MethodBase? GetCallingMethod()
        {
            var trace = new StackTrace(fNeedFileInfo: false);
            for (int i = 1; i < trace.FrameCount; i++)
            {
                var method = trace.GetFrame(i)?.GetMethod();
                if (method?.GetCustomAttribute<StackTraceHiddenAttribute>() is not null)
                    continue;

                var declaringType = method?.DeclaringType;

                if (declaringType == null)
                    continue;

                var assemblyName = declaringType.Assembly.GetName().Name;

                if (assemblyName != null &&
                    !assemblyName.StartsWith("Serilog", StringComparison.OrdinalIgnoreCase) &&
                    !assemblyName.StartsWith("System", StringComparison.OrdinalIgnoreCase) &&
                    !assemblyName.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase))
                {
                    var name = declaringType.Name;

                    if (name.Contains('<') || name.Contains('>') || name == "EngineLoggerEnricher")
                    {
                        continue;
                    }

                    return method;
                }
            }
            return null;
        }
    }
     
    internal class DebuggerSink(string template) : ILogEventSink
    {
        private readonly MessageTemplateTextFormatter _formatter = new(template);

        public void Emit(LogEvent logEvent)
        {
            using var stringWriter = new StringWriter();
            _formatter.Format(logEvent, stringWriter);

            System.Diagnostics.Debug.Write(stringWriter.ToString());
        }
    }
}
