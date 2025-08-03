using System.Collections.ObjectModel;
using System.Text.Json;
using FmodAudio;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core;
using RE.Rendering;
using RE.Utils;
using Serilog;

namespace RE.Audio
{
    internal static class SoundManager
    {
        public static ReadOnlyDictionary<string, List<string>> SoundMap;
        public static ReadOnlyCollection<Sound> ActiveSounds => _activeSounds.AsReadOnly();

        private static readonly Dictionary<string, List<string>> _soundMap = new();
        private static readonly Dictionary<string, FmodAudio.Sound> _buffers = new();
        private static readonly List<Sound> _activeSounds = new();
        public static FmodSystem FmodSystem;

        public static void Init()
        {
            Game.Instance.UpdateFrame += Update;

            Fmod.SetLibraryLocation("Dll");
            FmodSystem = Fmod.CreateSystem();
            FmodSystem.Init(256);

            Log.Information($"FMOD Version: {FmodSystem.Version}");


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
            var pos = cam.Position.ToSystemVector3();
            var forward = System.Numerics.Vector3.Normalize(cam.Front.ToSystemVector3());
            var right = -System.Numerics.Vector3.Normalize(Vector3.Cross(forward.ToOpenTkVector3(), cam.Up).ToSystemVector3());
            var up = System.Numerics.Vector3.Cross(right, forward); // точно ортогонален
            var vel = System.Numerics.Vector3.Zero;


            FmodSystem.Update();
            FmodSystem.Set3DListenerAttributes(0, in pos, in vel, in forward, in up);

            foreach (var sound in _activeSounds.Where(s => s is { IsRelative: false }))
            {
                break; //fixme:
                float distance = Vector3.Distance(sound.Position, cam.Position);

                float gain;
                if (distance >= sound.MaxDistance)
                {
                    gain = 0f;
                }
                else if (distance <= sound.ReferenceDistance)
                {
                    gain = 1f;
                }
                else
                {
                    float range = sound.MaxDistance - sound.ReferenceDistance;
                    gain = 1f - ((distance - sound.ReferenceDistance) / range);
                    gain = Math.Clamp(gain, 0f, 1f);
                }

                var sPos = sound.Position.ToSystemVector3();
                var sZero = Vector3.Zero.ToSystemVector3();
                sound.FmodChannel.Set3DAttributes(in sPos, in sZero, in sZero);
                sound.FmodChannel.Volume = gain * sound.Volume;
            }
        }
        public static Sound Get(string id, int? n = null, bool in3d = false, bool loop = false)
        {
            if (!_soundMap.TryGetValue(id, out var files) || files.Count == 0)
            {
                Log.Error($"Sound ID {id} not found in the sound map.");
                return null!;
            }

            var file = files[n ?? Random.Shared.Next(files.Count)];

            if (!_buffers.TryGetValue(file, out FmodAudio.Sound fmodSound))
            {
                var flags = (in3d ? Mode._3D : Mode._2D) | (loop ? Mode.Loop_Normal : Mode.Loop_Off);
                fmodSound = FmodSystem.CreateSound(file, flags);
                _buffers[file] = fmodSound;
            }

            var sound = new Sound(fmodSound);
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

            var sound = Get(id, variant, settings.InWorld, settings.Loop);

            sound.Volume = settings.Volume;
            sound.Pitch = settings.Pitch;
            sound.Position = settings.SourcePosition ?? Vector3.Zero;
            sound.DisposeOnStop = settings.DisposeOnStop;
            sound.ShowDebugInfo = true;//settings.ShowDebugInfo;

            if (settings.InWorld)
            {
                sound.ReferenceDistance = settings.ReferenceDistance;
                sound.IsRelative = false;
                sound.MaxDistance = settings.MaxDistance;
                //if (!settings.UseLinearFading)
                //    sound.RollOff = settings.RollOff;
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
            if (sound == null)
                return;
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
    }
}
