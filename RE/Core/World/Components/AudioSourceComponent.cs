using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using DotRecast.Core.Collections.Extensions;
using OpenCvSharp.Internal.Vectors;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core.Audio;
using RE.Core.Scripting.Attributes;
using RE.Rendering;
using RE.Utils;
using Log = Serilog.Log;

namespace RE.Core.World.Components
{
    public class AudioSourceComponent : Component
    {
        [SerializerCallsConstructor]
        public class AudioClip
        {
            public AudioClip(string name, string audioEvent)
            {
                Name = name;
                AudioEvent = audioEvent;
                if (!SoundManager.Exists(AudioEvent))
                {
                    Log.Error("Audio event not found: {AudioPath}", AudioEvent);
                    return;
                }

                Instance = SoundManager.GetEvent(AudioEvent);
            }

            [EditorProperty] public string Name { get; }
            [EditorProperty] public string AudioEvent { get; }

            [EditorProperty]
            public bool Is3D
            {
                get;
                set
                {
                    field = value;
                    if (value && Instance is { Is3D: false })
                        Log.Warning("Audio event {Event} is not a 3D sound", AudioEvent);
                }
            } = true;

            [EditorProperty, If(nameof(Is3D), true)]
            public Vector3 LocalOffset { get; set; } = Vector3.Zero;

            [JsonIgnore] public Sound? Instance { get; }
        }

        [EditorProperty] public bool PlayOnStart { get; set; } = false;

        [EditorProperty, If(nameof(LockPositionToOwner), false)]
        public Vector3 Position { get; set; } //todo: remove this and make it locked to owner pos

        [EditorProperty] public bool LockPositionToOwner { get; set; } = false;

        [JsonPropertyName("Clips"), JsonInclude]
        private Dictionary<string, AudioClip> _clips = [];

        public override void Start()
        {
            if (PlayOnStart)
            {
                foreach (var (_, audioClip) in _clips)
                {
                    audioClip.Instance?.Play();
                }
            }
        }

        public override void Update(double delta)
        {
            foreach (var (_, audioClip) in _clips)
                audioClip.Instance?.Position =
                    audioClip.Is3D
                        ? ((LockPositionToOwner ? Owner.Transform.Position : Position) + audioClip.LocalOffset)
                        : Camera.GetActiveCamera().Position;
        }


        public void AddClip(string clipName, string eventName) =>
            AddClip(clipName, new AudioClip(clipName, eventName));

        public void AddClip(string clipName, AudioClip audioClip)
        {
            if (!_clips.TryAdd(clipName, audioClip))
                throw new InvalidOperationException($"Audio clip {clipName} already exists");
        }

        public void RemoveClip(AudioClip audioClip) => RemoveClip(audioClip.Name);

        public void RemoveClip(string clipName)
        {
            if (!_clips.Remove(clipName))
                Log.Warning("Tried to remove unexistent clip {ClipName}", clipName);
        }

        public void Play(string clip)
        {
            if (_clips.TryGetValue(clip, out var audioClip))
                audioClip.Instance?.Play();
            else Log.Error("{Object}: Audio clip {Clip} was not found", Owner, clip);
        }

        public void PlayOneShot(string clip)
        {
            if (_clips.TryGetValue(clip, out var audioClip))
            {
                if (audioClip.Is3D)
                    SoundManager.PlayOneShotEvent(audioClip.AudioEvent,
                        (LockPositionToOwner ? Owner.Transform.Position : Position) + audioClip.LocalOffset);
                else
                    SoundManager.PlayOneShotEvent(audioClip.AudioEvent);
            }
            else Log.Error("{Object}: Audio clip {Clip} was not found", Owner, clip);
        }


        public void Stop(string clip)
        {
            if (_clips.TryGetValue(clip, out var audioClip))
                audioClip.Instance?.Stop();
            else Log.Error("{Object}: Audio clip {Clip} was not found", Owner, clip);
        }

        public void StopAll() => _clips.ForEach(audio => audio.Value.Instance?.Stop());

        public override void Destroy() => _clips.ForEach(audio => audio.Value.Instance?.Dispose());

        /// <inheritdoc />
        public override JsonObject GetSaveData()
        {
            var obj = new JsonObject
            {
                { nameof(PlayOnStart), PlayOnStart },
                { nameof(Position), Position.ToJsonArray() },
                { nameof(LockPositionToOwner), LockPositionToOwner },
            };
            var clips = new JsonObject();
            foreach (var clip in _clips)
                clips.Add(clip.Key, SerializeClip(clip.Value));

            obj.Add("Clips", clips);
            return obj;
        }

        private JsonObject SerializeClip(AudioClip clip)
        {
            var o = new JsonObject();
            o.Add("args", new JsonArray { clip.Name, clip.AudioEvent });
            o.Add(nameof(clip.Is3D), clip.Is3D);
            o.Add(nameof(clip.LocalOffset), clip.LocalOffset.ToJsonArray());

            return o;
        }
    }
}