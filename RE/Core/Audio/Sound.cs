using System.Diagnostics;
using System.Runtime.CompilerServices;
using OpenTK.Mathematics;
using RE.Fmod;
using RE.Utils;
using Serilog;
using EventInstance = RE.Fmod.Studio.EventInstance;
using RESULT = RE.Fmod.RESULT;
using STOP_MODE = RE.Fmod.Studio.STOP_MODE;

namespace RE.Core.Audio
{
    public class Sound : IDisposable
    {
        private EventInstance _sound;

        public Vector3 Position
        {
            get
            {
                Assert(_sound.get3DAttributes(out var attributes));
                return attributes.position.ToOpenTkVector3();
            }
            set
            {
                Assert(_sound.get3DAttributes(out var attributes));
                Assert(_sound.set3DAttributes(attributes with { position = value.ToFmodVector3() }));
            }
        }

        internal Sound(EventInstance sound)
        {
            _sound = sound;
        }

        public void Play()
        {
            Assert(_sound.start());
        }

        public void Stop(bool immediate = true)
        {
            Assert(_sound.stop(immediate ? STOP_MODE.IMMEDIATE : STOP_MODE.ALLOWFADEOUT));
        }

        public void SetParameter(string name, float value)
        {
            Assert(_sound.setParameterByName(name, value));
        }

        ~Sound()
        {
            Destroy(false);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Destroy(true);
        }

        private void Destroy(bool disposing)
        {
            if (!_sound.isValid())
                return;

            if (disposing)
            {
                Assert(_sound.stop(STOP_MODE.IMMEDIATE));
            }

            Assert(_sound.release());

            _sound = default;
        }

        [StackTraceHidden]
        private static void Assert(RESULT result, [CallerArgumentExpression(nameof(result))] string exp = "")
        {
            if (result != RESULT.OK)
                Log.Error("{Expression}: {Result}", exp, Error.String(result));

            System.Diagnostics.Debug.Assert(result == RESULT.OK, $"'{exp}' != {nameof(RESULT.OK)}");
        }
    }
}