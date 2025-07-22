using System.Text.Json.Nodes;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Audio;
using RE.Core.Scripting;
using RE.Core.World.Components;
using RE.Debug.Overlay;

namespace RE.Core.World.Testing
{
    internal class Door : Component
    {
        private GameObject _door;
        private bool shouldOpen, used;

        [EditorProperty("State")] public float State { get; set; }

        public override void Start()
        {
            _door = new GameObject();
            _door.DoNotSave = true;
            _door.Parent = Owner;
            _door.Transform = (Transform)Owner.Transform.Clone();
            _door.Components.Add(new BoxColliderComponent());
            _door.Components.Add(new MeshComponent("assets/models/crate.fbx"));
            _door.Components.Add(new UsableComponent()
            {
                OnUsed = () =>
                {
                    sound?.Stop();
                    sound = SoundManager.Play("doors/doormove", new SoundPlaybackSettings()
                    {
                        VariantIndex = 0,
                        DisposeOnStop = true,
                        InWorld = true,
                        SourcePosition = _door.Transform.Position,
                        Volume = 0.5f
                    });
                    used = true;
                    shouldOpen = !shouldOpen;
                }
            });
            SceneManager.CurrentScene.GameObjects.Add(_door);
        }

        private bool _wasOpen = false; // предыдущее логическое состояние
        private Sound sound;

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

            // Ограничиваем State в допустимом диапазоне
            State = Math.Clamp(State, 0f, 1f);

            // Обновляем позицию двери
            _door.SetPosition(Owner.Transform.Position + new Vector3(0, State * Owner.Transform.Scale.Y * 2, 0));

            // Проверяем переходы состояний
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
            sound?.Stop();
            sound = SoundManager.Play("doors/doorstop", new SoundPlaybackSettings()
            {
                VariantIndex = 0,
                DisposeOnStop = true,
                InWorld = true,
                SourcePosition = _door.Transform.Position,
                Volume = 0.5f
            });

        }
        void OnClosed() { OnOpened(); }

        public override JsonNode GetSaveData()
        {
            var root = new JsonObject { { "State", State } };
            return root;
        }
    }
}
