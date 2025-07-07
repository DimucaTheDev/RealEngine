using NAudio.Wave;
using OpenTK.Audio.OpenAL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core;
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
            Game.Instance.UpdateFrame += Update;

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
            var cam = Camera.Instance;
            Vector3 pos = cam.Position;
            Vector3 forward = cam.Front.Normalized();
            Vector3 up = cam.Up.Normalized();
            AL.Listener(ALListener3f.Position, pos.X, pos.Y, pos.Z);
            AL.Listener(ALListenerfv.Orientation, [forward.X, forward.Y, forward.Z, up.X, up.Y, up.Z]);

            foreach (var sound in _activeSounds.Where(s => s is { IsRelative: false, UseLinearFading: true })) //only 3d sounds
            {
                float distance = Vector3.Distance(sound.Position, cam.Position);

                if (distance >= sound.MaxDistance)
                {
                    AL.Source(sound.Source, ALSourcef.Gain, 0f);
                    continue;
                }
                float range = sound.MaxDistance - sound.ReferenceDistance;
                float gain;

                if (distance <= sound.ReferenceDistance)
                    gain = 1f;
                else
                    gain = 1f - ((distance - sound.ReferenceDistance) / range);

                gain = Math.Clamp(gain, 0f, 1f);

                AL.Source(sound.Source, ALSourcef.Gain, gain * sound.Volume);
            }
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
            if (source == 0)
                Log.Error("Failed to generate OpenAL source.");
            AL.Source(source, ALSourcei.Buffer, buffer);

            var sound = new Sound(source);
            _activeSounds.Add(sound);

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

            sound.Volume = settings.Volume;
            sound.Pitch = settings.Pitch;
            sound.Loop = settings.Loop;
            sound.Position = settings.SourcePosition ?? Vector3.Zero;
            sound.DisposeOnStop = settings.DisposeOnStop;
            sound.ShowDebugInfo = settings.ShowDebugInfo;

            if (settings.InWorld)
            {
                sound.IsRelative = false;
                sound.ReferenceDistance = settings.ReferenceDistance;
                sound.MaxDistance = settings.MaxDistance;
                if (!settings.UseLinearFading)
                    sound.RollOff = settings.RollOff;
            }
            else
            {
                sound.IsRelative = true;
            }
            sound.Play();
            return sound;
        }

        //you can use Sound.Dispose(), but this method will also remove sound from the active list
        public static void DisposeSound(Sound? sound)
        {
            if (sound == null) return;
            sound.Dispose();
            _activeSounds.Remove(sound);
        }
        public static void StopAll()
        {
            foreach (var sound in _activeSounds.ToList())
            {
                sound.Dispose();
            }
            _activeSounds.Clear();
        }
        //todo: warn if sound cant be played in 3d
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
