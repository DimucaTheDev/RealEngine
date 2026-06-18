using RE.Editor.NodeEditor.Pins;
using Serilog;
using Serilog.Events;

namespace RE.Editor.NodeEditor.Nodes
{
    internal class LogNode : ActionNode
    {
        public override string Name => "Log";

        private readonly DataNodePin<LogEventLevel> _level;
        private readonly DataNodePin<string> _message;

        public LogNode()
        {
            _level = new DataNodePin<LogEventLevel>("Level");
            _message = new DataNodePin<string>("Message");
            InputPins.Add(_message);
            InputPins.Add(_level);
            ExecuteAction = _ =>
            {
                Log.Write(_level.Value, _message.Value!);
            };
        } 
    }
}