using OpenTK.Windowing.Common;

namespace RE.Core
{
    internal static class Time
    {
        public static DateTime StartTime { get; private set; } = DateTime.Now;
        public static DateTime LastUpdate { get; private set; } = StartTime;
        public static float DeltaTime { get; private set; }
        public static TimeSpan TotalTime => DateTime.Now - StartTime;

        private static bool _initialized;

        public static void Init()
        {
            if (_initialized)
            {
                Console.WriteLine($"Tried to init {nameof(Time)} again!");
                return;
            }
            StartTime = DateTime.Now;
            LastUpdate = StartTime;
            _initialized = true;
        }
        public static void Update(FrameEventArgs args)
        {
            if (!_initialized) Init();
            var now = DateTime.Now;
            LastUpdate = now;
            DeltaTime = (float)args.Time;
        }
    }
}
