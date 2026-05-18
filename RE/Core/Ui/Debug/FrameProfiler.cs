using System.Diagnostics;

namespace RE.Core.Ui.Debug
{
    public static class FrameProfiler
    {

        public sealed class ProfilerNode
        {
            public string Name = "";
            public double Start;
            public double End;
            public List<ProfilerNode> Children = new();
        }

        private static readonly Stopwatch _sw = new();
        private static readonly Stack<ProfilerNode> _stack = new();
        private static ProfilerNode _lastFrame = new();

        public static ProfilerNode Root { get; private set; } = new();

        public static ProfilerNode GetLastFrame() => _lastFrame ?? Root;

        internal static int UpdateDelay = 20;

        public static void BeginFrame()
        {
            Root = new ProfilerNode
            {
                Name = "Frame",
                Start = 0
            };

            _sw.Restart();
            _stack.Clear();
            _stack.Push(Root);
        }

        public static void EndFrame()
        {
            Root.End = _sw.Elapsed.TotalMilliseconds;
            if (Time.ElapsedFrames % UpdateDelay == 0)
                _lastFrame = Root;
        }

        public static void Begin(string name)
        {
            var parent = _stack.Peek();

            var node = new ProfilerNode
            {
                Name = name,
                Start = _sw.Elapsed.TotalMilliseconds
            };

            parent.Children.Add(node);
            _stack.Push(node);
        }

        public static void End()
        {
            var node = _stack.Pop();
            node.End = _sw.Elapsed.TotalMilliseconds;
        }
    }
}