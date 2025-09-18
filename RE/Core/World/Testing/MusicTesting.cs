using System.Text.Json.Nodes;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Audio;
using RE.Core.Scripting;
using RE.Core.World.Components;
using RE.Rendering;

namespace RE.Core.World.Testing
{
    internal class MusicTesting : Component
    {
        private BillboardTextComponent textState;
        private bool ActionRunning = false;
        private Queue<Action> testQueue = new();

        public override void OnSceneLoaded(Scene scene)
        {
            Camera.Instance.Position = new Vector3(0, 5, 0);
            Camera.Instance.Pitch = Camera.Instance.Yaw = 0;
            Camera.Instance.FirstMove = false;

            textState = Owner.Scene.GameObjects.FindByName("state").GetComponent<BillboardTextComponent>()!;

            Time.Schedule(3000, StartBenchmark);
        }

        private const string soundPath = "common/wpn_select";
        void StartBenchmark()
        {
            AddTask("Play 2D sound", () =>
            {
                SoundManager.Play(soundPath, new SoundPlaybackSettings
                {
                    InWorld = false
                });
            });

            AddTask("Play 3D sound left", () =>
            {
                SoundManager.Play(soundPath, new SoundPlaybackSettings
                {
                    InWorld = true,
                    Position = new Vector3(0, 0, -5)
                });
            });

            AddTask("Play 3D sound right", () =>
            {
                SoundManager.Play(soundPath, new SoundPlaybackSettings
                {
                    InWorld = true,
                    Position = new Vector3(0, 0, 5)
                });
            });

            AddTask("Play moving sound", () =>
            {
                var emitter = SoundManager.Play("test/test", new SoundPlaybackSettings
                {
                    InWorld = true,
                    Position = new Vector3(0, 0, 0)
                });

                float elapsed = 0f;
                Time.OnUpdateUntil(
                    predicate: dt => (elapsed += dt) >= emitter.Length - 0.2f,
                    action: dt =>
                    {
                        emitter.Position = new Vector3(MathF.Sin(elapsed * 2f) * 5f, 4, 7 * MathF.Sin(elapsed * 3f));
                    });
            }, 5000);

            AddTask("Play looping sound\nstop after 1s", () =>
            {
                var looping = SoundManager.Play(soundPath, new SoundPlaybackSettings
                {
                    InWorld = false,
                    Loop = true,
                    Volume = 0.2f
                });

                Time.Schedule(1000, () =>
                {
                    looping.Stop();
                });
            });
            AddTask("Back to home...", () =>
            {
                Time.Schedule(1000, () =>
                {
                    CommandHandler.ExecuteCommand("level lobby");
                });
            });
        }

        void AddTask(string name, Action action, int ms = 1000)
        {
            testQueue.Enqueue(() =>
            {
                ActionRunning = true;
                textState.Text = "Task: " + name;

                action();

                Time.Schedule(ms, () =>
               {
                   ActionRunning = false;
               });
            });
        }

        public override void Update(FrameEventArgs args)
        {
            if (!ActionRunning && testQueue.TryDequeue(out var task))
                task();
        }


        public override JsonNode GetSaveData()
        {
            throw new NotImplementedException();
        }
    }
}
