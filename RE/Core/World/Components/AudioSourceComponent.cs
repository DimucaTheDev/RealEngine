using System.Text.Json.Nodes;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core.Audio;
using RE.Core.Scripting.Attributes;
using RE.Rendering;
using Log = Serilog.Log;

namespace RE.Core.World.Components
{
    public class AudioSourceComponent : Component
    {
        [EditorProperty] public string AudioEvent { get; set; }
        [EditorProperty] public bool PlayOnStart { get; set; } = true;
        [EditorProperty] public bool IsInWorld { get; set; } = false;

        [EditorProperty, If(nameof(IsInWorld), true)]
        public Vector3 Position { get; set; }

        private Sound? _sound;

        public override void Start()
        {
            if (!SoundManager.Exists(AudioEvent))
            {
                Log.Error("Audio event not found: {AudioPath}", AudioEvent);
                return;
            }

            _sound = SoundManager.GetEvent(AudioEvent);
            if (PlayOnStart)
                _sound.Play();
        }

        public override void Update(FrameEventArgs args)
        {
            _sound?.Position = IsInWorld ? Position : Camera.GetActiveCamera().Position;
        }

        public void Play() => _sound?.Play();

        public override void OnDestroy()
        {
            SoundManager.StopAll();
        }
    }
}