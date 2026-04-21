using System.Text.Json.Nodes;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core.Audio;
using RE.Core.Scripting.Attributes;
using RE.Core.World.Components;
using RE.Core.World.Components.Physics;
using SceneEditor = RE.Editor.SceneEditor;

namespace RE.Core.World.Testing
{
#pragma warning disable
    internal class Door : Component
    {
        private GameObject _door;
        private bool shouldOpen, used;
        private bool _wasOpen;
        private Sound sound;

        [EditorProperty("State")] public float State { get; set; }

        public override void Start()
        {
            if (_door != null!)
                return;
            _door = new GameObject();
            _door.DoNotSave = true;
            _door.DoNotShowInEditor = true;
            _door.Parent = Owner;
            _door.Transform = (Transform)Owner.Transform.Clone(); 
            _door.Components.Add(new BoxColliderComponent());
            _door.Components.Add(new MeshComponent("assets/models/crate.fbx"));
            _door.Components.Add(new UsableComponent
            {
                OnUsed = () =>
                {
                    /*
                    if (sound?.IsReady ?? false)
                        sound.Stop();
                    sound = SoundManager.Play("doors/doormove", new SoundPlaybackSettings
                    {
                        VariantIndex = 0,
                        DisposeOnStop = true,
                        InWorld = true,
                        Position = _door.Transform.Position,
                        Volume = 0.5f
                    });*/
                    used = true;
                    shouldOpen = !shouldOpen;
                }
            });
            SceneManager.CurrentScene.GameObjects.Add(_door);
        }


        public override void Update(FrameEventArgs args)
        {
            float f = 0;

            if (!SceneEditor.Enabled && used)
            {
                if (shouldOpen && State < 0.85f)
                {
                    f = Time.DeltaTime / 2;
                    State += f;
                }
                else if (!shouldOpen && State > 0)
                {
                    f = -Time.DeltaTime / 2;
                    State += f;
                }
            }

            State = Math.Clamp(State, 0f, 1f);

            _door.SetPosition(Owner.Transform.Position + new Vector3(0, State * Owner.Transform.Scale.Y * 2, 0));

            if (!_wasOpen && State >= 0.85f)
            {
                OnOpened();
                _wasOpen = true;
            }
            else if (_wasOpen && State <= 0.001f)
            {
                OnClosed();
                _wasOpen = false;
            }
        }

        void OnOpened()
        {
            /*sound?.Stop();
            sound = SoundManager.Play("doors/doorstop", new SoundPlaybackSettings()
            {
                VariantIndex = 0,
                DisposeOnStop = true,
                InWorld = true,
                Position = _door.Transform.Position,
                Volume = 0.5f
            });*/
        }
        void OnClosed() { OnOpened(); }

        public override JsonNode GetSaveData()
        {
            var root = new JsonObject { { "State", State } };
            return root;
        }
    }
}
