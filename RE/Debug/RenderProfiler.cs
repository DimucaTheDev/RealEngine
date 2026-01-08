using System.Collections.Concurrent;
using System.Diagnostics;
using RE.Core;

namespace RE.Debug
{
    public class RenderProfiler
    {
        private static readonly ConcurrentDictionary<string, RenderProfiler> _running = new();
        private static readonly ConcurrentDictionary<string, double> _latestValues = new();
        private readonly Stopwatch _sw = Stopwatch.StartNew();

        public string Name { get; private set; }

        private RenderProfiler() { }

        public static readonly List<(long timestamp, string e)> Events = [];

        public static RenderProfiler StartNew(string name)
        {
            if (!_running.TryAdd(name, new RenderProfiler() { Name = name }))
                throw new InvalidOperationException($"Profiler '{name}' is already started.");

            return _running[name];
        }

        public static void AddEvent(string name)
        {
            Events.Add((Time.ElapsedFrames, name));
        }

        public static double GetLast(string name)
        {
            return _latestValues.GetValueOrDefault(name, 0);
        }

        public static double Stop(string name)
        {
            if (!_running.TryRemove(name, out var profiler))
                throw new InvalidOperationException("Unknown profiler.");

            profiler._sw.Stop();

            var e = profiler._sw.Elapsed.TotalSeconds;
            _latestValues[name] = e;
            return e;
        }

        public static IReadOnlyDictionary<string, double> StopAll()
        {
            var result = new Dictionary<string, double>();

            foreach (var kv in _running.ToArray())
            {
                if (_running.TryRemove(kv.Key, out var profiler))
                {
                    profiler._sw.Stop();
                    var swElapsed = profiler._sw.Elapsed;
                    _latestValues[kv.Key] = swElapsed.TotalSeconds;
                    result[kv.Key] = swElapsed.TotalSeconds;
                }
            }

            return result;
        }

        public void Stop()
        {
            Stop(Name);
        }
    }
}