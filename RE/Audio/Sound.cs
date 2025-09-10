using System.Diagnostics.CodeAnalysis;
using FmodAudio;
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

        private bool _disposed;
        private float? _length = null!;
        private Time.ScheduledTask? _task;
        private readonly CircleRenderer _crRefDis;
        private readonly CircleRenderer _crMaxDis;
        private readonly SpriteRenderer _sprite;

        private float _maxDistance, _refDistance;
        private Vector3 _position;

        public event Action? Playing;
        public event Action? Paused;
        public event Action? Stopped;
        public event Action? Resumed;
        public event Action<float>? VolumeChanged;

        public FmodAudio.Sound FmodSound { get; private set; }
        public FmodAudio.Channel FmodChannel { get; private set; }

        public float Volume
        {
            get
            {
                if (!IsReady)
                {
                    Log.Error("Sound is not ready");
                    return default;
                }
                return FmodChannel.Volume;
            }
            set
            {
                if (!IsReady)
                {
                    Log.Error("Sound is not ready");
                    return;
                }
                if (value < 0)
                {
                    Log.Warning("Volume cant be negative. Clamping to 0");
                    value = 0;
                }
                FmodChannel.Volume = value;
                VolumeChanged?.Invoke(value);
            }
        }

        public float Offset
        {
            get
            {
                if (!IsReady)
                {
                    Log.Error("Sound is not ready");
                    return default;
                }
                return FmodChannel.GetPosition(TimeUnit.MS) / 1000f;
            }
            set
            {
                if (!IsReady)
                {
                    Log.Error("Sound is not ready");
                    return;
                }
                FmodChannel.SetPosition(TimeUnit.MS, (uint)(value * 1000f));
            }
        }

        public bool Loop
        {
            get
            {
                if (!IsReady)
                {
                    Log.Error("Sound is not ready");
                    return default;
                }
                return (FmodChannel.Mode & Mode.Loop_Normal) != 0;
            }
            set
            {
                if (!IsReady)
                {
                    Log.Error("Sound is not ready");
                    return;
                }
                if (value)
                    _task.TerminateIfScheduled();
                else if (IsPlaying)
                {
                    _task?.TerminateIfScheduled();
                    _task = Time.Schedule((int)((Length - Offset) * 1000), () => Stopped?.Invoke());
                }

                FmodChannel.Mode = value ? Mode.Loop_Normal : Mode.Loop_Off;
            }
        }

        public float Pitch
        {
            get
            {
                if (!IsReady)
                {
                    Log.Error("Sound is not ready");
                    return default;
                }
                return FmodChannel.Pitch;
            }
            set
            {
                if (!IsReady)
                {
                    Log.Error("Sound is not ready");
                    return;
                }
                if (value < 0 || value > 10.0f)
                {
                    Log.Warning("Pitch value must be between 0 and 10! Clamping to {Clamped}", Math.Clamp(value, 0f, 10f));
                    value = Math.Clamp(value, 0f, 10f);
                }
                FmodChannel.Pitch = value;
            }
        }

        public Vector3 Position
        {
            get
            {
                if (!IsReady)
                {
                    Log.Error("Sound is not ready");
                    return default;
                }
                if (IsRelative)
                    return _position;
                FmodChannel.Get3DAttributes(out var pos, out _, out _);
                return pos.ToOpenTkVector3();
            }
            set
            {
                if (!IsReady)
                {
                    Log.Error("Sound is not ready");
                    return;
                }

                _position = value;

                if (IsRelative)
                    return;

                _crMaxDis.Center = value;
                _crRefDis.Center = value;
                _sprite.Position = value;
                var zero = Vector3.Zero.ToSystemVector3();
                var pos = value.ToSystemVector3();
                FmodChannel.Set3DAttributes(in pos, in zero, in zero);
            }
        }

        public float Length
        {
            get
            {
                if (!IsReady)
                {
                    Log.Error("Sound is not ready");
                    return default;
                }
                if (_length == null)
                {
                    try
                    {
                        var pcm = FmodSound.GetLength(TimeUnit.PCM);
                        var frequency = FmodChannel.Frequency;
                        _length = pcm / frequency;
                    }
                    catch (DivideByZeroException)
                    {
                        Log.Error($" Something is off... i cant get frequency!!!");
                    }
                }
                return _length ?? 0;
            }
        }

        /// <summary>
        /// <c>true</c> if 2D. <c>false</c> if 3D.
        /// </summary>
        public bool IsRelative
        {
            get
            {
                if (!IsReady)
                {
                    Log.Error("Sound is not ready");
                    return default;
                }
                return (FmodChannel.Mode & Mode._2D) != 0;
            }
            set
            {
                if (!IsReady)
                {
                    Log.Error("Sound is not ready");
                    return;
                }

                var mode = FmodChannel.Mode;

                if (!value)
                {
                    mode &= ~Mode._2D;
                    mode |= Mode._3D;

                    //Refresh values

                    MaxDistance = _maxDistance;
                    ReferenceDistance = _refDistance;
                    Position = _position;
                }
                else
                {
                    mode &= ~(Mode._3D | Mode._3D_CustomRolloff | Mode._3D_HeadRelative |
                              Mode._3D_IgnoreGeometry | Mode._3D_InverseRolloff | Mode._3D_InverseTaperedRolloff |
                              Mode._3D_LinearRolloff | Mode._3D_LinearSquareRolloff | Mode._3D_WorldRelative);
                    mode |= Mode._2D;
                }

                FmodChannel.Mode = mode;
            }
        }

        public float MaxDistance
        {
            get
            {
                if (!IsReady)
                {
                    Log.Error("Sound is not ready");
                    return default;
                }
                if (IsRelative)
                    return _maxDistance;
                FmodChannel.Get3DMinMaxDistance(out _, out var distance);
                return distance;
            }
            set
            {
                if (!IsReady)
                {
                    Log.Error("Sound is not ready");
                    return;
                }
                if (value == 0)
                {
                    Log.Error("MaxDistance={Zero} bruh", 0);
                    return;
                }
                _maxDistance = value;

                if (IsRelative)
                    return;

                _crMaxDis.Radius = value;
                _maxDistance = value;
                FmodChannel.Set3DMinMaxDistance(ReferenceDistance, value);
            }
        }

        public float ReferenceDistance
        {
            get
            {
                if (!IsReady)
                {
                    Log.Error("Sound is not ready");
                    return default;
                }
                if (IsRelative)
                    return _refDistance;
                FmodChannel.Get3DMinMaxDistance(out var distance, out _);
                return distance;
            }
            set
            {
                if (!IsReady)
                {
                    Log.Error("Sound is not ready");
                    return;
                }
                _refDistance = value;
                if (IsRelative)
                    return;

                _crRefDis.Radius = value;
                FmodChannel.Set3DMinMaxDistance(value, MaxDistance);
            }
        }

        public bool ShowDebugInfo
        {
            get => _crRefDis.IsRendering() || _crMaxDis.IsRendering() || _sprite.IsRendering();
            set
            {
                if (value)
                {
                    _crMaxDis.StartRender();
                    _crRefDis.StartRender();
                    _sprite.StartRender();
                }
                else
                {
                    _crMaxDis.StopRender();
                    _crRefDis.StopRender();
                    _sprite.StopRender();
                }
            }
        }

        public SoundState State
        {
            get
            {
                if (!IsReady)
                {
                    Log.Error("Sound is not ready");
                    return default;
                }
                if (FmodChannel.IsPlaying)
                    return SoundState.Playing;
                if (FmodChannel.Paused)
                    return SoundState.Paused;
                return SoundState.Stopped;
            }
        }

        public bool DisposeOnStop { get; set; }
        public bool IsPlaying => State == SoundState.Playing;
        public bool IsPaused => State == SoundState.Paused;
        public bool IsStopped => State == SoundState.Stopped;
        public bool IsReady => FmodChannel != null! && FmodSound != null! && !_disposed && IsHandleValid();

        internal Sound(FmodAudio.Sound source)
        {
            FmodSound = source;
            FmodChannel = SoundManager.FmodSystem.PlaySound(source, paused: true)!;

            _sprite = new SpriteRenderer(Position, "Assets/Sprites/Editor/speaker.png");
            _crMaxDis = new CircleRenderer(Vector3.Zero, 0);
            _crRefDis = new CircleRenderer(Vector3.Zero, 0);

            MaxDistance = 10;
            ReferenceDistance = 1;
            Volume = 1.0f;
            Pitch = 1.0f;
            IsRelative = false;
            Position = Vector3.Zero;
            ShowDebugInfo = false;
            Loop = false;
            //DisposeOnStop = true;

            Playing += () => _sprite.ChangeTexture("Assets/sprites/editor/speaker_play.png");
            Stopped += () => _sprite.ChangeTexture("Assets/sprites/editor/speaker.png");
            Stopped += () =>
            {
                if (DisposeOnStop)
                    SoundManager.DisposeSound(this);
            };
            Paused += () => _sprite.ChangeTexture("Assets/sprites/editor/speaker.png");
            Resumed += () => _sprite.ChangeTexture("Assets/sprites/editor/speaker_play.png");
            VolumeChanged += (volume) =>
            {
                _sprite.ChangeTexture(volume == 0
                    ? "Assets/Sprites/Editor/speaker_mute.png"
                    : "Assets/Sprites/Editor/speaker.png");
            };
        }

        internal void Play()
        {
            if (!IsReady)
            {
                Log.Error("Performing action on disposed/non init-ed sound instance");
                return;
            }

            Playing?.Invoke();
            FmodChannel.Paused = false;
            _task.TerminateIfScheduled();
            if (!Loop)
                _task = Time.Schedule((int)(Length * 1000), () => Stopped?.Invoke());
        }
        public void Pause()
        {
            if (!IsReady)
            {
                Log.Error("Performing action on disposed/non init-ed sound instance");
                return;
            }

            _task.TerminateIfScheduled();
            FmodChannel.Paused = true;
            Paused?.Invoke();
        }
        public void Stop()
        {
            if (!IsReady)
            {
                Log.Error("Performing action on disposed/non init-ed sound instance");
                return;
            }

            _task.TerminateIfScheduled();
            FmodChannel.Stop();
            Stopped?.Invoke();
        }
        public void Resume()
        {
            if (!IsReady)
            {
                Log.Error("Performing action on disposed/non init-ed sound instance");
                return;
            }
            if (!IsPlaying)
            {
                _task?.TerminateIfScheduled();
                FmodChannel.Paused = false;
                if (!Loop)
                    _task = Time.Schedule((int)((Length - Offset) * 1000), () => Stopped?.Invoke());
                Resumed?.Invoke();
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            if (IsHandleValid())
                FmodChannel.Stop();
            FmodChannel = null!;
            _sprite.Dispose();
            _crRefDis.Dispose();
            _crMaxDis.Dispose();
            _disposed = true;
        }

        [SuppressMessage("ReSharper", "UnusedVariable")]
        private bool IsHandleValid()
        {
            try
            {
                var test = FmodChannel.Audibility;
                return true;
            }
            catch (FmodException e)
            {
                Log.Error(e, "Invalid FMOD handle! Exception: ");
                return false;
            }
        }
    }
}
