using System.Text.Json.Nodes;
using ImGuiNET;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core.Scripting;
using RE.Debug.Overlay;
using RE.Rendering.Renderables;

namespace RE.Core.World.Components
{
    [ComponentInfo("World", Description = "This component emits particles. Open 'Particle Editor' to change particle's style and behaviour")]
    internal class ParticleEmitterComponent : Component, IEditorPopup
    {
        public bool ShowPreview
        {
            get;
            set
            {
                field = value;
                if (!value)
                    OnDestroy();
            }
        } = false;
        public float SpawnRate { get; set; } = 50f;
        public int MaxParticles { get; set; } = 100;

        private readonly List<ParticleInstance> _particles = new();
        private float _spawnAccumulator = 0f;
        private const int TotalPhases = 11;
        private static readonly Dictionary<int, string> PhaseTextures = Enumerable.Range(0, TotalPhases)
            .ToDictionary(i => i, i => $"assets/big_smoke_{i}.png");
        private SpriteRenderer _spriteRenderer;
        private Vector3 _emitterPosition => Owner.Transform.Position;

        [EditorButton]
        public void OpenParticleEditor()
        {
            open = true;
        }

        public override void Start()
        {
            _spriteRenderer = new SpriteRenderer(Vector3.Zero, "assets/sprites/editor/emitter.png", scale: 0.75f);
        }

        public override void Render(FrameEventArgs args)
        {
            if (SceneEditor.Enabled && !ShowPreview)
            {
                _spriteRenderer.Position = _emitterPosition;
                _spriteRenderer.Render(args);
                return;
            }


            foreach (var particle in _particles)
            {
                particle.Sprite.Position = particle.Particle.Position;
                particle.Sprite.Render(args);
            }
        }

        public override void Update(FrameEventArgs args)
        {
            if (SceneEditor.Enabled && !ShowPreview)
                return;

            float dt = (float)args.Time;
            _spawnAccumulator += SpawnRate * dt;

            int spawnCount = (int)_spawnAccumulator;
            _spawnAccumulator -= spawnCount;

            for (int i = 0; i < spawnCount && _particles.Count < MaxParticles; i++)
                _particles.Add(new()
                {
                    Particle = CreateParticle(),
                    Sprite = new SpriteRenderer(_emitterPosition, PhaseTextures[0], scale: 1f)
                });

            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                var p = _particles[i];
                p.Particle.Lifetime -= dt;

                int phase = (int)(((1f - (p.Particle.Lifetime / p.Particle.InitialLifetime)) * TotalPhases));
                phase = Math.Clamp(phase, 0, TotalPhases - 1);

                p.Sprite.ChangeTexture(PhaseTextures[phase]); //todo

                if (p.Particle.Lifetime <= 0)
                {
                    _particles[i].Sprite.Dispose();
                    _particles.RemoveAt(i);
                    continue;
                }

                p.Particle.Position += p.Particle.Velocity * dt;
                _particles[i] = p;
            }
        }

        public override void OnDestroy()
        {
            foreach (var particle in _particles.ToList())
            {
                particle.Sprite.Dispose();
                _particles.Remove(particle);
            }
        }

        public override JsonNode GetSaveData()
        {
            throw new NotImplementedException();
            //todo
            return new JsonObject();
        }
        private Particle CreateParticle()
        {
            return new Particle
            {
                Position = _emitterPosition,
                Velocity = new Vector3(
                    Random.Shared.NextSingle() - 0.5f,
                    Random.Shared.NextSingle(),
                    Random.Shared.NextSingle() - 0.5f
                ) * 2f,
                Lifetime = 2f,
                InitialLifetime = 2f,
                Size = 0.2f, //todo
                Color = Color4.Orange
            };
        }

        private bool open;
        public bool ShouldRenderPopup()
        {
            return open;
        }

        private ParticleSettings _settings = new();
        private bool _preview, _preview_temp;

        public void RenderPopup()
        {
            ImGui.Checkbox("Show Preview in Scene Editor", ref _preview);
            if (_preview != _preview_temp)
            {
                ShowPreview = _preview;
                _preview_temp = _preview;
            }

            ImGui.Separator();

            if (ImGui.BeginTable("##table", 2, ImGuiTableFlags.BordersInnerV))
            {
                RenderField("Max Particles:", ref _settings.MaxParticles, ref _settings.MaxParticles_temp);
                RenderField("Lifetime:", ref _settings.Lifetime, ref _settings.Lifetime_temp);
                RenderField("Emission:", ref _settings.Emission, ref _settings.Emission_temp);
                RenderField("Angle:", ref _settings.Angle, ref _settings.Angle_temp);
                RenderField("Speed:", ref _settings.Speed, ref _settings.Speed_temp);
                RenderField("PosVar X:", ref _settings.PosVarX, ref _settings.PosVarX_temp);

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.Text("Gravity:");
                ImGui.TableSetColumnIndex(1);
                ImGui.PushItemWidth(50);
                ImGui.SliderFloat("X##gravityx", ref _settings.GravityX_temp, -100, 100);
                ImGui.SameLine();
                ImGui.SliderFloat("Y##gravityy", ref _settings.GravityY_temp, -100, 100);
                ImGui.PopItemWidth();

                RenderField("AccelRad:", ref _settings.AccelRad, ref _settings.AccelRad_temp);
                RenderField("AccelTan:", ref _settings.AccelTan, ref _settings.AccelTan_temp);

                ImGui.EndTable();
            }

            ApplyIfChanged(ref _settings.GravityX, ref _settings.GravityX_temp);
            ApplyIfChanged(ref _settings.GravityY, ref _settings.GravityY_temp);
        }

        private void RenderField(string label, ref int value, ref int temp)
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.Text(label);
            ImGui.TableSetColumnIndex(1);
            ImGui.SliderInt($"##{label}", ref temp, 0, 100);
            ApplyIfChanged(ref value, ref temp);
        }

        private void RenderField(string label, ref float value, ref float temp)
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.Text(label);
            ImGui.TableSetColumnIndex(1);
            ImGui.SliderFloat($"##{label}", ref temp, -100, 100);
            ApplyIfChanged(ref value, ref temp);
        }

        private void ApplyIfChanged<T>(ref T field, ref T temp) where T : struct, IEquatable<T>
        {
            if (!field.Equals(temp))
            {
                field = temp;
                // Обновление данных в движке
            }
        }


        public PopupSettings GetPopupSettings()
        {
            return new() { Width = 400, Height = 300, Title = $"Particle Editor (0x{Owner.Id:x}, {Owner.Name})" };
        }


        private class ParticleInstance
        {
            public Particle Particle;
            public SpriteRenderer Sprite;
        }
        private struct Particle
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float Lifetime;
            public float InitialLifetime;
            public float Size;
            public Color4 Color;
        }
        private class ParticleSettings
        {
            public int MaxParticles = 30, MaxParticles_temp = 30;
            public float Lifetime = 1.0f, Lifetime_temp = 1.0f;
            public float Emission = 30, Emission_temp = 30;
            public float Angle = -90, Angle_temp = -90;
            public float Speed = 29, Speed_temp = 29;
            public float PosVarX = 11, PosVarX_temp = 11;
            public float GravityX = 0, GravityX_temp = 0;
            public float GravityY = 0, GravityY_temp = 0;
            public float AccelRad = 0, AccelRad_temp = 0;
            public float AccelTan = 0, AccelTan_temp = 0;
        }

    }
}
