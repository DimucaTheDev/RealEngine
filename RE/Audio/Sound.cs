using OpenTK.Audio.OpenAL;
using OpenTK.Mathematics;
using RE.Core;
using RE.Rendering;
using RE.Utils;
using Log = Serilog.Log;

namespace RE.Audio
{
    public class Sound : IDisposable
    {
        public enum SoundState
        {
            Playing,
            Paused,
            Stopped
        }

        private readonly int _source, _buffer;
        private bool _disposed;
        private float? _length = null!;
        private Time.ScheduledTask? _task;

        public event Action? Playing;
        public event Action? Paused;
        public event Action? Stopped;
        public event Action? Resumed;

        public int Source => _source;
        public int Buffer => _buffer;

        public float Volume
        {
            get => AL.GetSource(_source, ALSourcef.Gain);
            set
            {
                if (value < 0)
                {
                    Log.Warning("Volume cant be negative. Clamping to 0");
                    value = 0;
                }
                AL.Source(_source, ALSourcef.Gain, value);
            }
        }

        public float Offset
        {
            get => AL.GetSource(_source, ALSourcef.SecOffset);
            set => AL.Source(_source, ALSourcef.SecOffset, value);
        }

        public bool Loop
        {
            get => AL.GetSource(_source, ALSourceb.Looping);
            set
            {
                if (value)
                    _task.TerminateIfScheduled();
                else if (IsPlaying)
                    _task = Time.Schedule((int)((Length - Offset) * 1000), () => Stopped?.Invoke());
                AL.Source(_source, ALSourceb.Looping, value);
            }
        }

        public float Pitch
        {
            get => AL.GetSource(_source, ALSourcef.Pitch);
            set
            {
                if (value < 0.5f || value > 2.0f)
                {
                    Log.Warning($"Pitch value must be between 0.5 and 2.0! Clamping to {Math.Clamp(value, 0.5f, 2.0f)}");
                    value = Math.Clamp(value, 0.5f, 2.0f);
                }
                AL.Source(_source, ALSourcef.Pitch, value);
            }
        }

        //todo: fix 3d sound position
        public Vector3 Position
        {
            get
            {
                AL.GetSource(_source, ALSource3f.Position, out float x, out float y, out float z);
                return new Vector3(x, y, z);
            }
            set => AL.Source(_source, ALSource3f.Position, value.X, value.Y, value.Z);
        }

        public float Length
        {
            get
            {
                if (_length == null)
                {
                    var size = AL.GetBuffer(_buffer, ALGetBufferi.Size);
                    var channels = AL.GetBuffer(_buffer, ALGetBufferi.Channels);
                    var bits = AL.GetBuffer(_buffer, ALGetBufferi.Bits);
                    var frequency = AL.GetBuffer(_buffer, ALGetBufferi.Frequency);
                    var lengthInSamples = size * 8 / (channels * bits);
                    _length = (float)lengthInSamples / frequency;
                }

                return _length ?? 0;
            }
        }

        public SoundState State
        {
            get
            {
                var state = (ALSourceState)AL.GetSource(_source, ALGetSourcei.SourceState);
                return state switch
                {
                    ALSourceState.Playing => SoundState.Playing,
                    ALSourceState.Paused => SoundState.Paused,
                    _ => SoundState.Stopped
                };
            }
        }

        public bool IsPlaying => State == SoundState.Playing;
        public bool IsPaused => State == SoundState.Paused;
        public bool IsStopped => State == SoundState.Stopped;

        internal Sound(int source)
        {
            _source = source;
            if (!AL.IsSource(_source))
                Log.Error($"Invalid OpenAL source specified: {_source}");
            _buffer = AL.GetSource(_source, ALGetSourcei.Buffer);

            r = new SpriteRenderer(Position, "Assets/Sprites/Editor/speaker.png");
            r.Render();
        }

        private SpriteRenderer r;
        internal void Play()
        {
            Playing?.Invoke();
            AL.SourcePlay(_source);

            _task.TerminateIfScheduled();
            _task = Time.Schedule((int)(Length * 1000), () => Stopped?.Invoke());
        }
        public void Pause()
        {
            _task.TerminateIfScheduled();

            AL.SourcePause(_source);
            Paused?.Invoke();
        }
        public void Stop()
        {
            _task.TerminateIfScheduled();
            AL.SourceStop(_source);
            Stopped?.Invoke();
        }
        public void Resume()
        {
            if (!IsPlaying)
            {
                _task?.TerminateIfScheduled();
                AL.SourcePlay(_source);
                if (!Loop)
                    _task = Time.Schedule((int)((Length - Offset) * 1000), () => Stopped?.Invoke());
                Resumed?.Invoke();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            Stop();
            r.Dispose();
            //_task?.TerminateIfScheduled();
            AL.DeleteSource(_source);
            _disposed = true;
        }
    }
}
