using OpenTK.Mathematics;

namespace RE.Audio
{
    internal class Sound
    {
        private Sound() { }

        public double Volume { get; set; } = 1.0;
        public double Pitch { get; set; } = 1.0;
        public Vector3 Position { get; set; }

        public static Sound FromFile()
        {
            return new Sound();
        }
    }
}
