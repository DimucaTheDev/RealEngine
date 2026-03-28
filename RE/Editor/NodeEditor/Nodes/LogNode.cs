using RE.Editor.NodeEditor.Pins;
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
            //_level = new DataNodePin<LogEventLevel>("Level", this);
            //_message = new DataNodePin<string>("Message", this);
            InputPins.Add(_message);
            InputPins.Add(_level);
        }

       // public override void Execute()
        //{
       //     Log.Write(_level.Value, _message.Value!);
       // }
    }
}
