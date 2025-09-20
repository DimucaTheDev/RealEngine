using OpenTK.Mathematics;

namespace RE.Audio
{
    /// <summary>
    /// Represents settings for sound playback in a 3D or 2D environment.
    /// Allows configuring sound variant, position, volume, pitch, looping,
    /// spatial behavior, and debug options.
    /// </summary>
    public struct SoundPlaybackSettings()
    {
        /// <summary>
        /// Index of the sound variant to play. If null, a random variant will be selected.
        /// </summary>
        public int? VariantIndex { get; set; } = null;

        /// <inheritdoc cref="Sound.Position"/>
        public Vector3 Position { get; set; } = Vector3.Zero;

        /// <inheritdoc cref="Sound.Pitch"/>
        public float Pitch { get; set; } = 1f;

        /// <inheritdoc cref="Sound.Volume"/>
        public float Volume { get; set; } = 1f;

        /// <inheritdoc cref="Sound.Loop"/>
        public bool Loop { get; set; } = false;

        /// <summary>
        /// Indicates whether the sound should be spatialized in the world.
        /// If true, <see cref="Position"/> and distance settings are used.
        /// </summary>
        /// <remarks>
        /// Sounds with more than one channel (stereo) can not be played as in-world sounds.
        /// </remarks>
        public bool InWorld { get; set; } = false;

        /// <inheritdoc cref="Sound.MaxDistance"/>
        public float MaxDistance { get; set; } = 10f;

        /// <inheritdoc cref="Sound.ReferenceDistance"/>
        public float ReferenceDistance { get; set; } = 1f;

        /// <inheritdoc cref="Sound.DisposeOnStop"/>
        public bool DisposeOnStop { get; set; } = true;

        /// <inheritdoc cref="Sound.DisposeOnStop"/>
        public bool ShowDebugInfo { get; set; } = false;
    }
}
