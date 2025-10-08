using System.Diagnostics.CodeAnalysis;
using FmodAudio;
using OpenTK.Mathematics;
using RE.Core;
using RE.Rendering.Renderables;
using RE.Rendering.Text;
using RE.Utils;
using FmodChannel = FmodAudio.Channel;
using FmodSound = FmodAudio.Sound;
using Log = Serilog.Log;

namespace RE.Audio
{
    /// <summary>
    /// Represents sound instance. Sound instance can be created using <see cref="SoundManager.Get(string, int?)"/> or <see cref="SoundManager.Play(string, SoundPlaybackSettings)"/> method.
    /// </summary>
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
        private Time.ScheduledTask? _task; // this task fires OnStopped event, because FMOD has no such event
        private readonly SphereLineRenderer _crRefDis;
        private readonly SphereLineRenderer _crMaxDis;
        private readonly SpriteRenderer _sprite;
        private readonly FloatingText _debugText;
        private float _maxDistance, _refDistance;
        private Vector3 _position;

        public event Action? Playing;
        public event Action? Paused;
        public event Action? Stopped;
        public event Action? Resumed;
        /// <summary>
        /// Occurs when the volume level changes.   
        /// </summary>
        /// <remarks>Subscribers receive the new volume level as a parameter when the event is raised. The
        /// volume value is typically represented as a floating-point number between 0.0 (muted) and 1.0 (maximum
        /// volume)</remarks>
        public event Action<float>? VolumeChanged;

        /// <summary>
        /// Stores the underlying FMOD sound instance.
        /// </summary>
        /// <remarks>
        /// <para>
        /// FMOD Docs: <a href="https://www.fmod.com/docs/2.01/api/core-api-sound.html"> https://www.fmod.com/docs/2.01/api/core-api-sound.html</a>
        /// </para>
        /// <para>
        /// Can be <see langword="null"/> if sound is disposed.
        /// </para>
        /// </remarks>
        public FmodSound FmodSound { get; }
        /// <summary>
        /// Stores the underlying FMOD channel instance.
        /// </summary>
        /// <remarks>
        /// <para>
        /// FMOD Docs: <a href="https://www.fmod.com/docs/2.01/api/core-api-channel.html"> https://www.fmod.com/docs/2.01/api/core-api-channel.html</a>
        /// </para>
        /// <para>
        /// Can be <see langword="null"/> if sound is disposed.
        /// </para>
        /// </remarks>
        public FmodChannel FmodChannel { get; private set; }

        /// <remarks>
        /// <para>Range: <c>[0; 1]</c>.</para>
        /// <para>Changing value calls <see cref="VolumeChanged"/> event with new volume level.</para>
        /// </remarks>
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

        /// <summary>
        /// Represents current playback position in seconds.
        /// </summary>
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

        /// <summary>
        /// Gets or sets a value indicating whether the sound should loop when playback reaches the end.
        /// </summary>
        /// <remarks>
        /// <para>If set to <see langword="true"/>, the sound will automatically restart from the
        /// beginning when it finishes playing. If set to <see langword="false"/>, playback will stop when the end is
        /// reached.
        /// </para>
        /// <para>
        /// Note that if property is set to <see langword="true"/>, the <see cref="Stopped"/> event will not be fired.
        /// </para>
        /// </remarks>
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

        /// <remarks>
        /// <para>Range: <c>[0; 10]</c>.</para>
        /// </remarks>
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

        /// <summary>
        /// Gets or sets the 3D position of the sound in world coordinates.
        /// </summary>
        /// <remarks>When <see cref="IsRelative"/> is set to <see langword="true"/>, changing this value will have no effect.
        /// </remarks>
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

                _crMaxDis.Center = value;
                _crRefDis.Center = value;
                _sprite.Position = value;

                if (IsRelative)
                    return;

                var zero = Vector3.Zero.ToSystemVector3();
                var pos = value.ToSystemVector3();
                FmodChannel.Set3DAttributes(in pos, in zero, in zero);
            }
        }

        /// <summary>
        /// Gets the length of the sound, in seconds.
        /// </summary>
        /// <remarks>If the sound is not ready, the property returns 0. The value is calculated based on
        /// the sound's PCM length and playback frequency. Subsequent accesses may return a cached value for
        /// performance.</remarks>
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
        /// Gets or sets a value indicating whether the sound is played in relative (2D) mode or spatialized (3D) mode.
        /// </summary>
        /// <remarks>When set to <see langword="true"/>, the sound is played as a 2D sound and is not
        /// affected by 3D positioning or distance attenuation. When set to <see langword="false"/>, the sound is
        /// spatialized in 3D space, and properties such as position and distance affect how it is heard. Changing this
        /// property may update related 3D sound parameters.</remarks>
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

        /// <summary>
        /// Gets or sets the maximum distance at which the sound can be heard in 3D space.
        /// </summary>
        /// <remarks><para>The maximum distance determines how far the sound will be audible before it is fully
        /// attenuated.</para>
        /// <para>Setting this property to zero is not allowed and will be ignored.</para>
        /// <para>This has no effect if <see cref="IsRelative"/> is set to <see langword="true"/></para></remarks>
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

        /// <summary>
        /// Gets or sets the reference distance for 3D sound attenuation.
        /// </summary>
        /// <remarks>
        /// <para>The reference distance determines how far listener can be away from <see cref="Position"/> to hear the sound
        /// at max volume.
        /// </para>
        /// <para>
        /// Setting this property to zero is not allowed and will be ignored.
        /// </para>
        /// </remarks>
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
                // todo: is it allowed to have ref dis > max dis?
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether debug information is displayed on the screen.
        /// </summary>
        /// <remarks>When set to <see langword="true"/>, enabling this property will display additional
        /// debug overlays or information relevant to the current context. Disabling it hides all such debug visuals.
        /// This info includes:
        /// <list type="bullet">
        /// <item>Red sphere of radius <see cref="MaxDistance"/></item>
        /// <item>Red sphere of radius <see cref="ReferenceDistance"/></item>
        /// <item>2D Sprite at <see cref="Position"/></item>
        /// <item>Floating text at <see cref="Position"/> with:
        /// <list type="bullet">
        /// <item>Position (see <see cref="Position"/>)</item>
        /// <item>State (see <see cref="SoundState"/>)</item>
        /// <item>Dispose on stop (see <see cref="DisposeOnStop"/>)</item>
        /// <item>Loop (see <see cref="Loop"/>)</item>
        /// <item>Offset (<see cref="Offset"/>)</item>
        /// <item>Length (<see cref="Length"/>)</item>
        /// </list>
        /// </item>
        /// </list>
        /// </remarks>
        public bool ShowDebugInfo
        {
            get => _debugText.IsRendering() || _crRefDis.IsRendering() || _crMaxDis.IsRendering() || _sprite.IsRendering();
            set
            {
                if (value)
                {
                    _crMaxDis.StartRender();
                    _crRefDis.StartRender();
                    _debugText.StartRender();
                    _sprite.StartRender();
                }
                else
                {
                    _debugText.StopRender();
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

        /// <summary>
        /// Gets or sets a value indicating whether the object should be disposed when the sound is stopped.
        /// <remarks>
        /// <para>If <see cref="Loop"/> is set to <see langword="true"/>, the sound instance will not be disposed.</para>
        /// </remarks>
        /// </summary>
        public bool DisposeOnStop { get; set; }
        public bool IsPlaying => State == SoundState.Playing;
        public bool IsPaused => State == SoundState.Paused;
        public bool IsStopped => State == SoundState.Stopped;

#pragma warning disable 8073
        /// <summary>
        /// Gets a value indicating whether the object is ready for use.
        /// </summary>
        /// <remarks>The object is considered ready when all required resources are initialized, and it has
        /// not been disposed. Use this property to check readiness before performing operations that depend on the
        /// object's valid state.</remarks>
        public bool IsReady => FmodChannel != null! && FmodSound != null! && !_disposed && IsHandleValid();
#pragma warning restore

        internal Sound(FmodAudio.Sound source)
        {
            FmodSound = source;
            FmodChannel = SoundManager.FmodSystem.PlaySound(source, paused: true)!;

            _sprite = new SpriteRenderer(Position, "Assets/Sprites/Editor/speaker.png");
            _crMaxDis = new SphereLineRenderer(Vector3.Zero, 0);
            _crRefDis = new SphereLineRenderer(Vector3.Zero, 0);
            _debugText = new("", Vector3.Zero, (FreeTypeFont)Fonts.Consolas);

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

        public void Play()
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

        /// <summary>
        /// Releases all resources used by the current instance.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Call this method when the instance is no longer needed to free unmanaged resources
        /// promptly. After calling this method, the instance should not be used.
        /// </para>
        /// <para>
        /// Sound automatically disposes when <see cref="DisposeOnStop"/> is set to <see langword="true"/>.
        /// </para>
        /// <para>
        /// It is recommended to use <see cref="SoundManager.DisposeSound"/> to properly remove the sound from active sound list
        /// </para>
        /// </remarks>
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
            _debugText.Dispose();
            _disposed = true;
        }

        internal void UpdateDebugInfo()
        {
            _debugText.Text = $"Pos: {Position}\nState: {State}\nDoS: {DisposeOnStop}\nLoop: {Loop}\n{Offset:0.00}/{Length:0.00} s";
            _debugText.Position = Position + (0, 1, 0);
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
