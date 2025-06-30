using NAudio.Wave;
using OpenTK.Audio.OpenAL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Rendering;
using Serilog;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace RE.Audio
{
    internal static class SoundManager
    {
        public static ReadOnlyDictionary<string, List<string>> SoundMap;

        private static readonly Dictionary<string, List<string>> _soundMap = new();
        private static readonly Dictionary<string, int> _buffers = new();
        private static readonly List<Sound> _activeSounds = new();
        private static ALDevice _device;
        private static ALContext _context;

        public static ReadOnlyCollection<Sound> ActiveSounds => _activeSounds.AsReadOnly();

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
            SoundMap = new ReadOnlyDictionary<string, List<string>>(_soundMap);
            Log.Information($"Mapped {_soundMap.Count} sounds.");
        }
        public static void Update(FrameEventArgs args)
        {
            var cam = Camera.Instance; // или откуда ты получаешь камеру
            Vector3 pos = cam.Position;
            Vector3 forward = cam.Front.Normalized();
            Vector3 up = cam.Up.Normalized();
            AL.Listener(ALListener3f.Position, pos.X, pos.Y, pos.Z);
            AL.Listener(ALListenerfv.Orientation, [
                forward.X, forward.Y, forward.Z,
                up.X,     up.Y,     up.Z
            ]);
        }
        public static Sound Get(string id, int? n = null)
        {
            if (!_soundMap.TryGetValue(id, out var files) || files.Count == 0)
            {
                Log.Error($"Sound ID {id} not found in the sound map.");
                return null!;
            }
            var file = files[n ?? Random.Shared.Next(files.Count)];

            if (!_buffers.TryGetValue(file, out int buffer))
            {
                buffer = Load(file);
                _buffers[file] = buffer;
            }

            int source = AL.GenSource();
            AL.Source(source, ALSourcei.Buffer, buffer);

            var sound = new Sound(source);
            sound.Playing += () => _activeSounds.Add(sound);
            sound.Stopped += () => _activeSounds.Remove(sound);

            return sound;
        }
        public static Sound Play(string id, SoundPlaybackSettings settings = default)
        {
            if (!_soundMap.TryGetValue(id, out var files) || files.Count == 0)
            {
                Log.Error($"Sound ID {id} not found in the sound map.");
                return null!;
            }

            var variant = settings.VariantIndex ?? Random.Shared.Next(files.Count);

            var sound = Get(id, variant);

            AL.Source(sound.Source, ALSourcef.Gain, settings.Volume);
            AL.Source(sound.Source, ALSourcef.Pitch, settings.Pitch);
            AL.Source(sound.Source, ALSourceb.Looping, settings.Loop);
            AL.Source(sound.Source, ALSource3f.Position, settings.SourcePosition?.X ?? 0, settings.SourcePosition?.Y ?? 0, settings.SourcePosition?.Z ?? 0);

            if (settings.InWorld)
            {
                AL.Source(sound.Source, ALSourceb.SourceRelative, false);
                AL.Source(sound.Source, ALSourcef.ReferenceDistance, settings.ReferenceDistance);
                AL.Source(sound.Source, ALSourcef.MaxDistance, settings.MaxDistance);
                AL.Source(sound.Source, ALSourcef.RolloffFactor, settings.RollOff);
            }

            sound.Play();
            return sound;
        }
        public static void StopAll()
        {
            foreach (var sound in _activeSounds.ToList())
            {
                sound.Dispose();
            }
            _activeSounds.Clear();
        }
        public static unsafe int Load(string path)
        {
            using var reader = new WaveFileReader(path);

            if (reader.WaveFormat.Encoding != WaveFormatEncoding.Pcm)
            {
                Log.Error($"Only PCM WAV is supported. Detected: {reader.WaveFormat.Encoding} ({path}).");
                return -1;
            }

            int numChannels = reader.WaveFormat.Channels;
            int bitsPerSample = reader.WaveFormat.BitsPerSample;
            int sampleRate = reader.WaveFormat.SampleRate;

            ALFormat formatAL = (numChannels, bitsPerSample) switch
            {
                (1, 8) => ALFormat.Mono8,
                (1, 16) => ALFormat.Mono16,
                (2, 8) => ALFormat.Stereo8,
                (2, 16) => ALFormat.Stereo16,
                _ => throw new NotSupportedException($"Unsupported WAV format: {numChannels}ch, {bitsPerSample}bit")
            };

            // Читаем все аудиоданные в буфер
            byte[] data = new byte[reader.Length];
            int bytesRead = reader.Read(data, 0, data.Length);

            if (bytesRead != data.Length)
                throw new InvalidOperationException("Failed to read entire WAV data.");

            int buffer = AL.GenBuffer();
            fixed (byte* ptr = data)
            {
                AL.BufferData(buffer, formatAL, (IntPtr)ptr, data.Length, sampleRate);
            }

            return buffer;
        }
    }
}
