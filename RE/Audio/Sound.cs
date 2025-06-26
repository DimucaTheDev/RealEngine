using OpenTK.Audio.OpenAL;
using OpenTK.Mathematics;

namespace RE.Audio
{
    public class Sound : IDisposable
    {
        private readonly int _source;
        private bool _disposed;

        public event Action? Playing;
        public event Action? Played;

        public float Volume
        {
            get => AL.GetSource(_source, ALSourcef.Gain);
            set => AL.Source(_source, ALSourcef.Gain, value);
        }

        public float Pitch
        {
            get => AL.GetSource(_source, ALSourcef.Pitch);
            set => AL.Source(_source, ALSourcef.Pitch, value);
        }

        public Vector3 Position
        {
            get
            {
                AL.GetSource(_source, ALSource3f.Position, out float x, out float y, out float z);
                return new Vector3(x, y, z);
            }
            set => AL.Source(_source, ALSource3f.Position, value.X, value.Y, value.Z);
        }

        internal Sound(int source)
        {
            _source = source;
        }

        internal void Play()
        {
            Playing?.Invoke();
            AL.SourcePlay(_source);
            // Optionally: Add async monitoring to raise Played when finished
        }

        public void Stop()
        {
            AL.SourceStop(_source);
            Played?.Invoke();
        }

        public void Dispose()
        {
            if (_disposed) return;
            AL.DeleteSource(_source);
            _disposed = true;
        }
    }
}
