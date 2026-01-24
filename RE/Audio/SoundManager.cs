using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FMOD;
using FMOD.Studio;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core;
using RE.Core.Assets;
using RE.Rendering;
using RE.Utils;
using Serilog;
using Result = FMOD.RESULT;

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
        public static FMOD.System FmodSystem;
        public static FMOD.Studio.System StudioSystem;

        public const int MaxChannels = 256;

        private static bool _initialized;
        private static Dictionary<string, List<string>> _soundMap = new();
        private static readonly Dictionary<string, FMOD.Sound> _buffers = new();
        private static readonly List<Sound> _activeSounds = new();
        private static readonly List<string> MissingSounds = [];
        private static readonly List<string> AudioFileExtensions = [".wav", ".mp3", ".ogg", ".flac", ".aiff"];

        private static readonly DEBUG_CALLBACK _fmodDebugCallback = (flags, filePtr, line, funcPtr, msgPtr) =>
        {
            var message = Marshal.PtrToStringAnsi(msgPtr)?.TrimEnd('\n');
            var file = Marshal.PtrToStringAnsi(filePtr);

            if (string.IsNullOrWhiteSpace(message))
                return RESULT.OK;

            if (flags.HasFlag(DEBUG_FLAGS.ERROR))
                Log.Error("[FMOD] {Message} (0x{Func:x} at {File}:{Line})", message, funcPtr, file, line);
            else if (flags.HasFlag(DEBUG_FLAGS.WARNING))
                Log.Warning("[FMOD] {Message}", message);
            else
                Log.Information("[FMOD] {Message}", message);


            return RESULT.OK;
        };

        /// <summary>
        /// Initialises FMOD sound subsystem.
        /// </summary>
        public static void Init()
        {
            Check(FMOD.Debug.Initialize(
                 DEBUG_FLAGS.ERROR | DEBUG_FLAGS.WARNING,
                DEBUG_MODE.CALLBACK,
                _fmodDebugCallback
            ));

            Check(FMOD.Studio.System.create(out StudioSystem));

            Check(StudioSystem.initialize(256, FMOD.Studio.INITFLAGS.LIVEUPDATE, FMOD.INITFLAGS.NORMAL, IntPtr.Zero));
            Check(StudioSystem.getCoreSystem(out FmodSystem));

            Check(FmodSystem.getVersion(out uint version));

            var resMaster = StudioSystem.loadBankMemory(ContentManager.GetBytes("assets/testing/bank/Master.bank"), LOAD_BANK_FLAGS.NORMAL, out var m);
            Log.Information("Master Bank: {Res}", resMaster);

            var resStrings = StudioSystem.loadBankMemory(ContentManager.GetBytes("assets/testing/bank/Master.strings.bank"), LOAD_BANK_FLAGS.NORMAL, out _);
            Log.Information("Strings Bank: {Res}", resStrings);

            Check(m.getEventList(out var array));
            foreach (var description in array)
            {
                Check(description.getPath(out var path));
                Console.WriteLine($"Event Found: {path}");
            }

            uint major = version >> 16;
            uint minor = (version >> 8) & 0xFF;
            uint patch = version & 0xFF;

            Log.Information("FMOD Version: {Mj}.{Mn}.{Dv}", major, minor, patch);

            var files = ContentManager.GetFiles("Assets/Audio", true)
                .Where(s => AudioFileExtensions.Contains(Path.GetExtension(s))).ToArray();

            _soundMap = ProcessFiles(files, "Assets/Audio");

            Log.Information("Mapped {Count} sounds.", _soundMap.Count);
            _initialized = true;
        }
        /// <summary>
        /// Updates listener position
        /// </summary>
        /// <param name="args">Time passed from previous <see cref="Game.OnUpdateFrame"/> call.</param>
        public static void Update(FrameEventArgs args)
        {
            if (!_initialized)
                return;

            var cam = Camera.GetActiveCamera();
            var pos = cam.Position.ToFmodVector3();
            var forward = cam.Front.Normalized().ToFmodVector3();
            var right = (-(Vector3.Cross(forward.ToOpenTkVector3(), cam.Up).Normalized())).ToFmodVector3();
            var up = System.Numerics.Vector3.Cross(right.ToSystemVector3(), forward.ToSystemVector3()).ToOpenTkVector3().ToFmodVector3();
            var vel = Vector3.Zero.ToFmodVector3();

            foreach (var sound in _activeSounds.Where(s => s.ShowDebugInfo))
            {
                sound.UpdateDebugInfo();
            }

            Check(StudioSystem.update());
            Check(FmodSystem.update());
            Check(FmodSystem.set3DListenerAttributes(0, ref pos, ref vel, ref forward, ref up));
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
                if (!MissingSounds.Contains(id))
                {
                    MissingSounds.Add(id);
                    Log.Error("Sound ID {Id} not found in the sound map.", id);
                }
                return null!;
            }

            var file = files[variation ?? Random.Shared.Next(files.Count)];

            if (!_buffers.TryGetValue(file, out FMOD.Sound fmodSound))
            {
                byte[] audioData = ContentManager.GetBytes(file).ToArray();

                var createsoundexinfo = new CREATESOUNDEXINFO
                {
                    cbsize = Marshal.SizeOf(typeof(CREATESOUNDEXINFO)),
                    length = (uint)audioData.Length
                };

                var result = FmodSystem.createSound(
                    audioData,
                    MODE.OPENMEMORY | MODE.CREATESAMPLE,
                    ref createsoundexinfo,
                    out fmodSound
                );

                if (result != Result.OK)
                {
                    Log.Error("Failed to create sound '{File}'. Error: {Result}", file, Error.String(result));
                    return null;
                }

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
                if (!MissingSounds.Contains(id))
                {
                    MissingSounds.Add(id);
                    Log.Error("Sound ID {Id} not found in the sound map.", id);
                }
                return null!;
            }

            var variant = settings.VariantIndex ?? Random.Shared.Next(files.Count);

            var sound = Get(id, variant);

            sound.Volume = settings.Volume;
            sound.Pitch = settings.Pitch;
            sound.Position = settings.Position;
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
            Log.Verbose("Playing sound {Id}: {Variant}", id, variant);
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

        private static void Check(Result result, [CallerArgumentExpression(nameof(result))] string exp = "")
        {
            if (result != RESULT.OK)
                Log.Error("{Expression}: {Result}", exp, Error.String(result));
            System.Diagnostics.Debug.Assert(result == RESULT.OK, $"'{exp}' != {nameof(RESULT.OK)}");
        }

        private static Dictionary<string, List<string>> ProcessFiles(IEnumerable<string> files, string basePath)
        {
            Dictionary<string, List<string>> fileMap = new();

            foreach (var file in files)
            {
                string nameWithoutExt = Path.GetFileNameWithoutExtension(file);

                int suffixStart = nameWithoutExt.Length;

                bool digitFound = false;
                for (int i = nameWithoutExt.Length - 1; i >= 0; i--)
                {
                    if (char.IsDigit(nameWithoutExt[i]))
                    {
                        digitFound = true;
                        suffixStart = i;
                    }
                    else if (digitFound)
                    {
                        suffixStart = i + 1;
                        break;
                    }
                }

                string cleanedName = nameWithoutExt.Substring(0, suffixStart);

                string? directory = Path.GetDirectoryName(file);
                string combinedPath = Path.Join(directory, cleanedName);

                string fileNameKey = Path.GetRelativePath("Assets/Audio", combinedPath
                    .Replace(basePath, "")
                    .Replace('\\', '/')
                    .TrimStart('/', '\\')
                    .TrimEnd('_', '-'));

                string relativePath = file
                    .Replace(basePath, "")
                    .Replace('\\', '/');

                string assetsAudioPath = $"Assets/Audio{relativePath}";

                if (!fileMap.TryGetValue(fileNameKey, out List<string>? list))
                {
                    list = new List<string>();
                    fileMap.Add(fileNameKey, list);
                }

                list.Add(assetsAudioPath);
            }

            return fileMap;
        }
    }
}
