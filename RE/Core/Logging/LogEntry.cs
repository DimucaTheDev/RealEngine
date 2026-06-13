using Serilog.Events;

namespace RE.Core.Logging;
public readonly record struct LogEntry(string Message, DateTimeOffset Timestamp, LogEventLevel Level); 