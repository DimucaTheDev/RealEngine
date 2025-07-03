using OpenTK.Mathematics;

namespace RE.Audio
{
    public struct SoundPlaybackSettings()
    {
        public int? VariantIndex { get; set; } = null;
        public Vector3? SourcePosition { get; set; } = Vector3.Zero;
        public float Pitch { get; set; } = 1f;
        public float Volume { get; set; } = 1f;
        public bool Loop { get; set; } = false;
        public bool InWorld { get; set; } = false;
        public float MaxDistance { get; set; } = 10f;
        public float RollOff { get; set; } = 1f;
        public float ReferenceDistance { get; set; } = 1f;
        public bool UseLinearFading { get; set; } = true;
        public bool DisposeOnStop { get; set; } = true;
        public bool ShowDebugInfo { get; set; } = false;
    }
}
