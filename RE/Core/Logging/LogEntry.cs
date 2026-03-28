using Serilog.Events;

namespace RE.Core.Logging
{
    public readonly struct LogEntry
    {
        public LogEntry(string message, DateTimeOffset timestamp, LogEventLevel level)
        {
            Message = message;
            Timestamp = timestamp;
            Level = level;
        }
        
        public readonly string Message;
        public readonly DateTimeOffset Timestamp;
        public readonly LogEventLevel Level;
    }
}