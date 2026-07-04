using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core.Assets;
using RE.Editor;
using RE.Fmod;
using RE.Rendering;
using RE.Utils;
using Serilog;
using DEBUG_CALLBACK = RE.Fmod.DEBUG_CALLBACK;
using DEBUG_FLAGS = RE.Fmod.DEBUG_FLAGS;
using DEBUG_MODE = RE.Fmod.DEBUG_MODE;
using Result = RE.Fmod.RESULT;
using FmodBank = RE.Fmod.Studio.Bank;
using INITFLAGS = RE.Fmod.Studio.INITFLAGS;
using LOAD_BANK_FLAGS = RE.Fmod.Studio.LOAD_BANK_FLAGS;
using STOP_MODE = RE.Fmod.Studio.STOP_MODE;
using Thread = System.Threading.Thread;

namespace RE.Core.Audio
{
    public static class SoundManager
    {
        private static Fmod.System _fmodSystem;
        private static Fmod.Studio.System _studioSystem;

        public const int MaxChannels = 256;

        private static readonly DEBUG_CALLBACK _fmodDebugCallback = (flags, filePtr, line, funcPtr, msgPtr) =>
        {
            var message = Marshal.PtrToStringAnsi(msgPtr)?.TrimEnd('\n');
            var file = Marshal.PtrToStringAnsi(filePtr);

            if (string.IsNullOrWhiteSpace(message))
                return Result.OK;

            if (flags.HasFlag(DEBUG_FLAGS.ERROR))
                Log.ForContext("SourceContext", "fmodL.dll")
                    .ForContext("ThreadName", "FMOD Audio Thread")
                    .Error("[FMOD] {Message} (0x{Func:x} at {File}:{Line})", message, funcPtr, file, line);
            else if (flags.HasFlag(DEBUG_FLAGS.WARNING))
                Log.ForContext("SourceContext", "fmodL.dll")
                    .ForContext("ThreadName", "FMOD Audio Thread")
                    .Warning("[FMOD] {Message}", message);
            else
                Log.ForContext("SourceContext", "fmodL.dll")
                    .ForContext("ThreadName", "FMOD Audio Thread")
                    .Information("[FMOD] {Message}", message);

            return Result.OK;
        };

        /// <summary>
        /// Initialises FMOD sound subsystem.
        /// </summary>
        public static void Init()
        {
            Assert(Fmod.Debug.Initialize(
                DEBUG_FLAGS.ERROR | DEBUG_FLAGS.WARNING,
                DEBUG_MODE.CALLBACK,
                _fmodDebugCallback
            ));

            var studioFlags = Debugger.IsAttached ? INITFLAGS.LIVEUPDATE : INITFLAGS.NORMAL;

            Assert(Fmod.Studio.System.create(out _studioSystem));
            Assert(_studioSystem.initialize(MaxChannels, studioFlags, INITFLAGS.NORMAL, IntPtr.Zero));
            Assert(_studioSystem.getCoreSystem(out _fmodSystem));
            Assert(_fmodSystem.getVersion(out uint version));

            int major = (int)(version >> 16);
            int minor = (int)((version >> 8) & 0xFF);
            int patch = (int)(version & 0xFF);

            Log.Information("[FMOD] Version: {Version}", new Version(major, minor, patch));
            Log.Debug("[FMOD] Live Update is {State}.",
                studioFlags.HasFlag(INITFLAGS.LIVEUPDATE) ? "Enabled" : "Disabled");

            LoadAllBanks();
        }

        /// <summary>
        /// Updates listener position
        /// </summary>
        /// <param name="args">Time passed from previous <see cref="Game.OnUpdateFrame"/> call.</param>
        public static void Update(double args)
        {
            if (!_studioSystem.isValid())
                return;

            var cam = SceneEditor.SimulationRunning ? Camera.Main : Camera.GetActiveCamera();
            var pos = cam.Position.ToFmodVector3();
            var forward = cam.Front.Normalized().ToFmodVector3();
            var right = (-(Vector3.Cross(forward.ToOpenTkVector3(), cam.Up).Normalized())).ToFmodVector3();
            var up = System.Numerics.Vector3.Cross(right.ToSystemVector3(), forward.ToSystemVector3()).ToOpenTkVector3()
                .ToFmodVector3();
            var vel = Vector3.Zero.ToFmodVector3();

            Assert(_fmodSystem.set3DListenerAttributes(0, ref pos, ref vel, ref forward, ref up));
            Assert(_studioSystem.setListenerAttributes(0,
                new ATTRIBUTES_3D() { position = pos, forward = forward, up = up }));
            Assert(_studioSystem.update());
            Assert(_fmodSystem.update());
        }

        private static FmodBank LoadBank(string resourcePath)
        {
            var buffer = ContentManager.GetBytes(resourcePath);

            Assert(_studioSystem.loadBankMemory(buffer, LOAD_BANK_FLAGS.NORMAL, out var bank));
            Assert(bank.getEventList(out var eventArray));

            Log.Verbose("[FMOD] Events in {Bank}: {@List}",
                resourcePath,
                eventArray
                    .Select(s =>
                    {
                        s.getPath(out var path);
                        return path;
                    }));

            Log.Debug("[FMOD] {Count} events in {Bank}", eventArray.Length, resourcePath);

            return bank;
        }

        public static Sound GetEvent(string eventName)
        {
            Assert(_studioSystem.getEvent(eventName, out var eventDescription));
            Assert(eventDescription.createInstance(out var instance));
            return new Sound(instance);
        }

        public static Sound PlayEvent(string eventName)
        {
            Assert(_studioSystem.getEvent(eventName, out var eventDescription));
            Assert(eventDescription.createInstance(out var instance));
            Assert(instance.start());
            return new Sound(instance);
        }

        public static void PlayOneShotEvent(string eventName)
        {
            Assert(_studioSystem.getEvent(eventName, out var eventDescription));
            Assert(eventDescription.createInstance(out var instance));
            Assert(instance.start());
            Assert(instance.release());
        }

        public static void PlayOneShotEvent(string eventName, Vector3 position)
        {
            Assert(_studioSystem.getEvent(eventName, out var eventDescription));
            Assert(eventDescription.createInstance(out var instance));
            Assert(instance.set3DAttributes(new ATTRIBUTES_3D
            {
                forward = Vector3.UnitZ.ToFmodVector3(),
                up = Vector3.UnitY.ToFmodVector3(),
                velocity = Vector3.Zero.ToFmodVector3(),
                position = position.ToFmodVector3()
            }));
            Assert(instance.start());
            Assert(instance.release());
        } 
        
        public static void StopAll(bool immediate = true)
        {
            Assert(_studioSystem.getBus("bus:/", out var bus));
            Assert(bus.stopAllEvents(immediate ? STOP_MODE.IMMEDIATE : STOP_MODE.ALLOWFADEOUT));
        }

        private static void LoadAllBanks()
        {
            var files = ContentManager.GetFiles("Assets/Audio");
            foreach (var file in files.Where(s => s.EndsWith(".strings.bank")))
            {
                LoadBank(file);
            }

            foreach (var file in files.Where(s => s.EndsWith(".bank") && !s.EndsWith(".strings.bank")))
            {
                LoadBank(file);
            }
        }

        [StackTraceHidden]
        private static void Assert(Result result, [CallerArgumentExpression(nameof(result))] string exp = "")
        {
            if (result != Result.OK)
                Log.Error("[FMOD] Assert failed: '{Expression}' returned {Result}", exp, Error.String(result));

            System.Diagnostics.Debug.Assert(result == Result.OK, $"'{exp}' != {nameof(Result.OK)}");
        }

        public static void Destroy()
        {
            if (_studioSystem.isValid())
            {
                Assert(_studioSystem.release());
            }

            _studioSystem.clearHandle();
            _fmodSystem.clearHandle();
        }

        public static bool Exists(string audioEvent)
        {
            return _studioSystem.getEvent(audioEvent, out _)
                is not (Result.ERR_FILE_NOTFOUND or Result.ERR_EVENT_NOTFOUND);
        }
    }
}