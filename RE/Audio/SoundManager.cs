using OpenTK.Audio.OpenAL;
using OpenTK.Mathematics;
using RE.Rendering.Camera;
using System.Text.Json;

namespace RE.Audio
{
    internal static class SoundManager
    {
        private static readonly Dictionary<string, List<string>> _soundMap = new();
        private static readonly Dictionary<string, int> _buffers = new();
        private static readonly List<Sound> _activeSounds = new();

        private static ALDevice _device;
        private static ALContext _context;

        public static void Init()
        {
            _device = ALC.OpenDevice(null);
            _context = ALC.CreateContext(_device, (int[])null!);
            ALC.MakeContextCurrent(_context);

            var path = Path.Combine("Assets", "soundmap.json");
            var json = File.ReadAllText(path);
            _soundMap.Clear();
            var map = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json);
            foreach (var kvp in map!)
            {
                _soundMap[kvp.Key] = kvp.Value;
            }
        }

        public static Sound Play(string id, Vector3 sourcePosition, float pitch = 1f, float gain = 1f, bool loop = false)
        {
            if (!_soundMap.TryGetValue(id, out var files) || files.Count == 0)
                throw new ArgumentException($"Sound ID '{id}' not found");

            var file = files[Random.Shared.Next(files.Count)];

            if (!_buffers.TryGetValue(file, out int buffer))
            {
                //buffer = AudioLoader.Load(file);
                _buffers[file] = buffer;
            }
            var listenerPosition = Camera.Instance.Position;

            int source = AL.GenSource();
            AL.Source(source, ALSourcei.Buffer, buffer);
            AL.Source(source, ALSourcef.Gain, gain);
            AL.Source(source, ALSourcef.Pitch, pitch);
            AL.Source(source, ALSourceb.Looping, loop);
            AL.Source(source, ALSource3f.Position, sourcePosition.X, sourcePosition.Y, sourcePosition.Z);

            AL.Listener(ALListener3f.Position, listenerPosition.X, listenerPosition.Y, listenerPosition.Z);

            var sound = new Sound(source);
            _activeSounds.Add(sound);
            sound.Play();
            return sound;
        }
    }
}
