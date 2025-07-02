using OpenTK.Audio.OpenAL;
using OpenTK.Mathematics;
using RE.Core;
using RE.Rendering.Renderables;
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
        private CircleRenderer crRefDis, crMaxDis;
        private SpriteRenderer sprite;


        public event Action? Playing;
        public event Action? Paused;
        public event Action? Stopped;
        public event Action? Resumed;
        public event Action<float>? VolumeChanged;

        public int Source => _source;
        public int Buffer => _buffer;

        public float Volume //add max volume for 
        {
            get => AL.GetSource(_source, ALSourcef.Gain);
            set
            {
                if (value < 0)
                {
                    Log.Warning("Volume cant be negative. Clamping to 0");
                    value = 0;
                }
                VolumeChanged?.Invoke(value);
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

        public Vector3 Position
        {
            get
            {
                AL.GetSource(_source, ALSource3f.Position, out float x, out float y, out float z);
                return new Vector3(x, y, z);
            }
            set
            {
                crMaxDis.Center = value;
                crRefDis.Center = value;
                sprite.Position = value;
                AL.Source(_source, ALSource3f.Position, value.X, value.Y, value.Z);
            }
        }

        public float Length
        {
            get
            {
                if (_length == null)
                {
                    try
                    {
                        var size = AL.GetBuffer(_buffer, ALGetBufferi.Size);
                        var channels = AL.GetBuffer(_buffer, ALGetBufferi.Channels);
                        var bits = AL.GetBuffer(_buffer, ALGetBufferi.Bits);
                        var frequency = AL.GetBuffer(_buffer, ALGetBufferi.Frequency);
                        var lengthInSamples = size * 8 / (channels * bits);
                        _length = (float)lengthInSamples / frequency;
                    }
                    catch (DivideByZeroException e)
                    {
                        Log.Error($"Unable to obtain 'channels', 'bits' or 'size'. Is _buffer({_buffer}) correct?");
                    }
                }
                return _length ?? 0;
            }
        }

        public bool IsRelative
        {
            get => AL.GetSource(_source, ALSourceb.SourceRelative);
            set
            {
                if (value)
                {
                    crMaxDis.StopRender();
                    crRefDis.StopRender();
                }
                else
                {
                    crMaxDis.Render();
                    crRefDis.Render();
                }
                AL.Source(_source, ALSourceb.SourceRelative, value);
            }
        }

        public float MaxDistance
        {
            get => AL.GetSource(_source, ALSourcef.MaxDistance);
            set
            {
                crMaxDis.Radius = value;
                AL.Source(_source, ALSourcef.MaxDistance, value);
            }
        }

        public float ReferenceDistance
        {
            get => AL.GetSource(_source, ALSourcef.ReferenceDistance);
            set
            {
                crRefDis.Radius = value;
                AL.Source(_source, ALSourcef.ReferenceDistance, value);
            }
        }

        public float RollOff
        {
            get => AL.GetSource(_source, ALSourcef.RolloffFactor);
            set
            {
                if (UseLinearFading && value != 0)
                    Log.Warning($"Setting {nameof(RollOff)} value to {value}, but {nameof(UseLinearFading)} is true");
                AL.Source(_source, ALSourcef.RolloffFactor, value);
            }
        }

        public bool ShowDebugInfo
        {
            get => crRefDis.IsRendering() || crMaxDis.IsRendering() || sprite.IsRendering();
            set
            {
                if (value)
                {
                    crMaxDis.Render();
                    crRefDis.Render();
                    sprite.Render();
                }
                else
                {
                    crMaxDis.StopRender();
                    crRefDis.StopRender();
                    sprite.StopRender();
                }
            }
        }

        public bool UseLinearFading { get; set; } = true;

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

            sprite = new SpriteRenderer(Position, "Assets/Sprites/Editor/speaker.png");
            crMaxDis = new CircleRenderer(Vector3.Zero, 0);
            crRefDis = new CircleRenderer(Vector3.Zero, 0);

            MaxDistance = 10;
            ReferenceDistance = 1;
            RollOff = 0f;
            Volume = 1.0f;
            Pitch = 1.0f;
            IsRelative = false;
            Position = Vector3.Zero;

            ShowDebugInfo = true;

            Playing += () => sprite.ChangeTexture("Assets/sprites/editor/speaker_play.png");
            Stopped += () => sprite.ChangeTexture("Assets/sprites/editor/speaker.png");
            Paused += () => sprite.ChangeTexture("Assets/sprites/editor/speaker.png");
            VolumeChanged += (volume) =>
            {
                sprite.ChangeTexture(volume == 0
                    ? "Assets/Sprites/Editor/speaker_mute.png"
                    : "Assets/Sprites/Editor/speaker.png");
            };
        }

        internal void Play()
        {
            Playing?.Invoke();
            AL.SourcePlay(_source);

            _task.TerminateIfScheduled();
            if (!Loop)
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
            sprite.Dispose();
            crRefDis.Dispose();
            crMaxDis.Dispose();
            AL.DeleteSource(_source);
            _disposed = true;
        }
    }
}
