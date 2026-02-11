using System.Diagnostics;
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
using ADVANCEDSETTINGS = FMOD.Studio.ADVANCEDSETTINGS;
using Result = FMOD.RESULT;
using FmodBank = FMOD.Studio.Bank;
using INITFLAGS = FMOD.Studio.INITFLAGS;

namespace RE.Audio
{
    public static unsafe class SoundManager
    {
        private static FMOD.System _fmodSystem;
        private static FMOD.Studio.System _studioSystem;

        public const int MaxChannels = 256;

        private static readonly DEBUG_CALLBACK _fmodDebugCallback = (flags, filePtr, line, funcPtr, msgPtr) =>
        {
            var message = Marshal.PtrToStringAnsi(msgPtr)?.TrimEnd('\n');
            var file = Marshal.PtrToStringAnsi(filePtr);

            if (string.IsNullOrWhiteSpace(message))
                return Result.OK;

            if (flags.HasFlag(DEBUG_FLAGS.ERROR))
                Log.Error("[FMOD] {Message} (0x{Func:x} at {File}:{Line})", message, funcPtr, file, line);
            else if (flags.HasFlag(DEBUG_FLAGS.WARNING))
                Log.Warning("[FMOD] {Message}", message);
            else
                Log.Information("[FMOD] {Message}", message);

            return Result.OK;
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

            var studioFlags = Debugger.IsAttached ? INITFLAGS.LIVEUPDATE : INITFLAGS.NORMAL;

            Check(FMOD.Studio.System.create(out _studioSystem));
            Check(_studioSystem.initialize(MaxChannels, studioFlags, FMOD.INITFLAGS.NORMAL, IntPtr.Zero));
            Check(_studioSystem.getCoreSystem(out _fmodSystem));
            Check(_fmodSystem.getVersion(out uint version));

            uint major = version >> 16;
            uint minor = (version >> 8) & 0xFF;
            uint patch = version & 0xFF;

            Log.Information("[FMOD] Version: {Mj}.{Mn}.{Dv}", major, minor, patch);
            Log.Debug("[FMOD] Live Update is {State}.",
                studioFlags.HasFlag(INITFLAGS.LIVEUPDATE) ? "Enabled" : "Not Enabled");

            LoadAllBanks();
        }

        /// <summary>
        /// Updates listener position
        /// </summary>
        /// <param name="args">Time passed from previous <see cref="Game.OnUpdateFrame"/> call.</param>
        public static void Update(FrameEventArgs args)
        {
            if (!_studioSystem.isValid())
                return;

            var cam = Camera.GetActiveCamera();
            var pos = cam.Position.ToFmodVector3();
            var forward = cam.Front.Normalized().ToFmodVector3();
            var right = (-(Vector3.Cross(forward.ToOpenTkVector3(), cam.Up).Normalized())).ToFmodVector3();
            var up = System.Numerics.Vector3.Cross(right.ToSystemVector3(), forward.ToSystemVector3()).ToOpenTkVector3().ToFmodVector3();
            var vel = Vector3.Zero.ToFmodVector3();

            Check(_studioSystem.update());
            Check(_fmodSystem.update());
            Check(_fmodSystem.set3DListenerAttributes(0, ref pos, ref vel, ref forward, ref up));
        }

        public static FmodBank LoadBank(string resourcePath)
        { 
            var buffer = ContentManager.GetBytes(resourcePath);

            Check(_studioSystem.loadBankMemory(buffer, LOAD_BANK_FLAGS.NORMAL, out var bank));
            Check(bank.getEventList(out var eventArray));

            Log.Debug("[FMOD] Events in {Bank}: {@List}",
                resourcePath,
                eventArray
                    .Select(s =>
            {
                s.getPath(out var path);
                return path;
            }));

            return bank;
        }

        public static Sound PlayEvent(string eventName)
        {
            Check(_studioSystem.getEvent(eventName, out var eventDescription));
            Check(eventDescription.createInstance(out var instance));
            Check(instance.start());
            return new Sound(instance);
        }

        public static void PlayOneShotEvent(string eventName)
        {
            Check(_studioSystem.getEvent(eventName, out var eventDescription));
            Check(eventDescription.createInstance(out var instance));
            Check(instance.start());
            Check(instance.release());
        }

        public static void StopAll(bool immediate = true)
        {
            Check(_studioSystem.getBus("bus:/", out var bus));
            Check(bus.stopAllEvents(immediate ? STOP_MODE.IMMEDIATE : STOP_MODE.ALLOWFADEOUT));
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
        private static void Check(Result result, [CallerArgumentExpression(nameof(result))] string exp = "")
        {
            if (result != RESULT.OK)
                Log.Error("[FMOD] Check failed: {Expression} returned {Result}", exp, Error.String(result));

            System.Diagnostics.Debug.Assert(result == RESULT.OK, $"'{exp}' != {nameof(RESULT.OK)}");
        }

        public static void Destroy()
        {
            if (_studioSystem.isValid())
            {
                Check(_studioSystem.release());
            }

            _studioSystem.clearHandle();
            _fmodSystem.clearHandle();
        }
    }
}
