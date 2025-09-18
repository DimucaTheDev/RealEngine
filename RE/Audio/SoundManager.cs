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
    public static class SoundManager
    {
        /// <summary>
        /// Gets the mapping of sound categories to their associated sound file names.
        /// </summary>
        /// <remarks>
        /// Paths in the sound map are relative to the application's working directory.
        /// </remarks>
        public static ReadOnlyDictionary<string, List<string>>? SoundMap;
        public static ReadOnlyCollection<Sound> ActiveSounds => _activeSounds.AsReadOnly();
        /// <summary>
        /// Represents the global instance of the FMOD audio system.
        /// </summary>
        /// <remarks>Use this field to access FMOD system-level functionality throughout the application.</remarks>
        public static FmodSystem FmodSystem;

        private static readonly Dictionary<string, List<string>> _soundMap = new();
        private static readonly Dictionary<string, FmodAudio.Sound> _buffers = new();
        private static readonly List<Sound> _activeSounds = new();

        public static void Init()
        {
            Game.Instance.UpdateFrame += Update;

            Fmod.SetLibraryLocation("Dll/Win32");
            FmodSystem = Fmod.CreateSystem();
            FmodSystem.Init(256);

            Log.Information("FMOD Version: {FmodVersion}", FmodSystem.Version);


            var path = Path.Combine("Assets", "soundmap.json");
            var json = File.ReadAllText(path);
            _soundMap.Clear();
            var map = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json);
            foreach (var kvp in map!)
            {
                _soundMap[kvp.Key] = kvp.Value;
            }
            SoundMap = new ReadOnlyDictionary<string, List<string>>(_soundMap);
            Log.Information("Mapped {Count} sounds.", _soundMap.Count);
        }
        public static void Update(FrameEventArgs args)
        {
            var cam = Camera.Instance;
            var pos = cam.Position.ToSystemVector3();
            var forward = System.Numerics.Vector3.Normalize(cam.Front.ToSystemVector3());
            var right = -System.Numerics.Vector3.Normalize(Vector3.Cross(forward.ToOpenTkVector3(), cam.Up).ToSystemVector3());
            var up = System.Numerics.Vector3.Cross(right, forward); // точно ортогонален
            var vel = System.Numerics.Vector3.Zero;

            foreach (var sound in _activeSounds.Where(s => s.ShowDebugInfo))
            {
                sound.UpdateDebugInfo();
            }

            FmodSystem.Update();
            FmodSystem.Set3DListenerAttributes(0, in pos, in vel, in forward, in up);

            return; //i dunno code below is kinda useless with FMOD
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

        /// <summary>
        /// Retrieves a sound instance associated with the specified sound identifier.
        /// </summary>
        /// <remarks>If multiple sound files are associated with the same identifier, the optional index
        /// parameter allows selection of a specific variation. If the index is not provided, a random variation is
        /// chosen. The returned sound instance is tracked for active playback management.</remarks>
        /// <param name="id">The unique identifier of the sound to retrieve. Cannot be null or empty.</param>
        /// <param name="variation">The index of the sound variation to retrieve. If null, a random variation is selected.</param>
        /// <returns>A new instance of the sound associated with the specified identifier and variation index, or <see langword="null"/> if the
        /// identifier is not found.</returns>
        public static Sound Get(string id, int? variation = null)
        {
            if (!_soundMap.TryGetValue(id, out var files) || files.Count == 0)
            {
                Log.Error("Sound ID {Id} not found in the sound map.", id);
                return null!;
            }

            var file = files[variation ?? Random.Shared.Next(files.Count)];

            if (!_buffers.TryGetValue(file, out FmodAudio.Sound fmodSound))
            {
                fmodSound = FmodSystem.CreateSound(file);
                _buffers[file] = fmodSound;
            }

            var sound = new Sound(fmodSound);
            _activeSounds.Add(sound);

            return sound;
        }

        /// <summary>
        /// Plays a sound with the specified identifier and playback settings.
        /// </summary>
        /// <remarks>If multiple sound variants are available for the given ID, the variant is selected
        /// randomly unless a specific variant index is provided in <paramref name="settings"/>. The returned <see
        /// cref="Sound"/> object can be used to control playback or query the state of the sound.</remarks>
        /// <param name="id">The unique identifier of the sound to play. Must correspond to a sound registered in the sound map.</param>
        /// <param name="settings">The playback settings to apply to the sound. If not specified, default settings are used.</param>
        /// <returns>A <see cref="Sound"/> instance representing the playing sound, or <see langword="null"/> if the specified
        /// sound ID is not found.</returns>
        public static Sound Play(string id, SoundPlaybackSettings settings = default)
        {
            if (!_soundMap.TryGetValue(id, out var files) || files.Count == 0)
            {
                Log.Error("Sound ID {Id} not found in the sound map.", id);
                return null!;
            }

            var variant = settings.VariantIndex ?? Random.Shared.Next(files.Count);

            var sound = Get(id, variant);

            sound.Volume = settings.Volume;
            sound.Pitch = settings.Pitch;
            sound.Position = settings.Position ?? Vector3.Zero;
            sound.DisposeOnStop = settings.DisposeOnStop;
            sound.ShowDebugInfo = settings.ShowDebugInfo;
            sound.Loop = settings.Loop;
            sound.IsRelative = !settings.InWorld;

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
            //Log.Debug($"Playing sound {id}:{variant}");
            return sound;
        }

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
