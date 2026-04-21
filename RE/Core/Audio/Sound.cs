using System.Diagnostics;
using System.Runtime.CompilerServices;
using RE.Fmod;
using Serilog;
using EventInstance = RE.Fmod.Studio.EventInstance;
using RESULT = RE.Fmod.RESULT;
using STOP_MODE = RE.Fmod.Studio.STOP_MODE;

namespace RE.Core.Audio
{
    public class Sound : IDisposable
    {
        private EventInstance _sound;

        internal Sound(EventInstance sound)
        {
            _sound = sound;
        }

        public void SetParameter(string name, float value)
        {
            Check(_sound.setParameterByName(name, value));
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
                Check(_sound.stop(STOP_MODE.IMMEDIATE));
            }
            Check(_sound.release());

            _sound = default;
        }

        [StackTraceHidden]
        private static void Check(RESULT result, [CallerArgumentExpression(nameof(result))] string exp = "")
        {
            if (result != RESULT.OK)
                Log.Error("{Expression}: {Result}", exp, Error.String(result));

            System.Diagnostics.Debug.Assert(result == RESULT.OK, $"'{exp}' != {nameof(RESULT.OK)}");
        }
    }
}
