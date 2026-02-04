using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using FMOD;
using Hexa.NET.ImGui.Backends.Vulkan;
using Serilog;
using static BulletSharp.DiscreteCollisionDetectorInterface;
using EventInstance = FMOD.Studio.EventInstance;

namespace RE.Audio
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
                Check(_sound.stop(FMOD.Studio.STOP_MODE.IMMEDIATE));
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
