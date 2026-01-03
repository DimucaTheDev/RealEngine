using System.Text.Json.Nodes;
using Hexa.NET.ImGui;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core.Scripting;
using RE.Core.Scripting.Attributes;
using RE.Editor;
using RE.Editor.Panels.Viewport;
using RE.Rendering.Renderables;
using SceneEditor = RE.Editor.SceneEditor;

namespace RE.Core.World.Components
{
    [ComponentInfo("World", Description = "This component emits particles. Open 'Particle Editor' to change particle's style and behaviour")]
    internal class ParticleEmitterComponent : Component, IEditorPopup, IEditorUpdate, IEditorRender
    {
        private bool _showPreview = false;

        public float SpawnRate { get; set; } = 50f;
        public int MaxParticles { get; set; } = 1000;

        private readonly List<ParticleInstance> _particles = new();
        private float _spawnAccumulator = 0f;
        private const int TotalPhases = 11;
        private static readonly Dictionary<int, string> PhaseTextures = Enumerable.Range(0, TotalPhases)
            .ToDictionary(i => i, i => $"assets/testing/big_smoke_{i}.png");
        private SpriteRenderer _emitterSpriteRenderer;
        private Vector3 _emitterPosition => Owner.Transform.Position;

        [EditorButton]
        public void OpenParticleEditor()
        {
            _openPopup = true;
        }

        public override void Start()
        {
            if (_emitterSpriteRenderer == null!)
                _emitterSpriteRenderer = new SpriteRenderer(Vector3.Zero, "assets/sprites/editor/emitter.png", scale: 0.75f);
        }

        public override void Render(FrameEventArgs args)
        {
            if (SceneEditor.Enabled && !SceneEditor.PreviewParticles)
            {
                _emitterSpriteRenderer.Position = _emitterPosition;
                _emitterSpriteRenderer.Render(args);
                return;
            }

            foreach (var particle in _particles)
            {
                particle.Sprite.Position = particle.Particle.Position;
                particle.Sprite.Render(args);
            }
        }

        private Random _rand = Random.Shared;

        public override void Update(FrameEventArgs args)
        {
            float dt = (float)args.Time;
            _spawnAccumulator += _settings.Emission * dt;

            int spawnCount = (int)_spawnAccumulator;
            _spawnAccumulator -= spawnCount;

            for (int i = 0; i < spawnCount && _particles.Count < _settings.MaxParticles; i++)
            {
                Vector3 position = _emitterPosition;
                position.X += RandomRange(-_settings.PosVarX, _settings.PosVarX);
                position.Y += RandomRange(-_settings.PosVarY, _settings.PosVarY);
                position.Z += RandomRange(-_settings.PosVarZ, _settings.PosVarZ);

                // Convert angles into radians
                float yaw = MathHelper.DegreesToRadians(_settings.Angle);    // горизонтальный (в плоскости X-Y)
                float pitch = MathHelper.DegreesToRadians(_settings.Angle3D); // вертикальный (вверх-вниз)

                // Calculate direction from spherical coordinates
                Vector3 direction = new Vector3(
                    (float)(Math.Cos(pitch) * Math.Cos(yaw)),
                    (float)(Math.Cos(pitch) * Math.Sin(yaw)),
                    (float)(Math.Sin(pitch))
                );

                // Добавить разброс скорости
                direction.X += RandomRange(-_settings.VelVarX, _settings.VelVarX);
                direction.Y += RandomRange(-_settings.VelVarY, _settings.VelVarY);
                direction.Z += RandomRange(-_settings.VelVarZ, _settings.VelVarZ);

                direction = Vector3.Normalize(direction) * _settings.Speed;

                var particle = new Particle
                {
                    Position = position,
                    Velocity = direction,
                    Lifetime = RandomRange(_settings.Lifetime - _settings.LifetimeVar, _settings.Lifetime + _settings.LifetimeVar),
                    InitialLifetime = _settings.Lifetime,
                    Size = RandomRange(_settings.SizeStart - _settings.SizeVar, _settings.SizeStart + _settings.SizeVar),
                    InitialSize = RandomRange(_settings.SizeStart - _settings.SizeVar, _settings.SizeStart + _settings.SizeVar),
                    Color = _settings.ColorStart
                };

                _particles.Add(new ParticleInstance
                {
                    Particle = particle,
                    Sprite = new SpriteRenderer(particle.Position, PhaseTextures[0], scale: particle.Size)
                });
            }


            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                var p = _particles[i];
                p.Particle.Lifetime -= dt;

                float lifeProgress = 1f - (p.Particle.Lifetime / p.Particle.InitialLifetime);
                int phase = (int)(lifeProgress * TotalPhases);
                phase = Math.Clamp(phase, 0, TotalPhases - 1);
                p.Sprite.ChangeTexture(PhaseTextures[phase]);

                float currentSize = MathHelper.Lerp(_settings.SizeStart, _settings.SizeEnd, lifeProgress);
                p.Particle.Size = currentSize;

                if (p.Particle.Lifetime <= 0)
                {
                    p.Sprite.Dispose();
                    _particles.RemoveAt(i);
                    continue;
                }

                Vector3 acceleration = Vector3.Zero;

                acceleration += new Vector3(_settings.GravityX, _settings.GravityY, _settings.GravityZ);

                Vector3 toCenter = Vector3.Zero - p.Particle.Position;
                if (toCenter.LengthSquared > 0.0001f)
                {
                    Vector3 radial = Vector3.Normalize(toCenter);
                    p.Particle.Velocity += radial * _settings.AccelRad * Time.DeltaTime;

                    Vector3 tangential = new Vector3(-radial.Y, radial.X, 0); // 2D только
                    p.Particle.Velocity += tangential * _settings.AccelTan * Time.DeltaTime;
                }



                p.Particle.Velocity *= (1.0f - _settings.Drag * dt);

                p.Particle.Velocity += acceleration * dt;
                p.Particle.Position += p.Particle.Velocity * dt;

                p.Sprite.Position = p.Particle.Position;
                _particles[i] = p;
            }
        }

        private float RandomRange(float min, float max)
        {
            return (float)(_rand.NextDouble() * (max - min) + min);
        }

        public override void OnDestroy()
        {
            foreach (var particle in _particles.ToList())
            {
                particle.Sprite.Dispose();
            }
            _particles.Clear();
        }

        public override JsonNode GetSaveData()
        {
            var json = new JsonObject();
            json[nameof(_settings)] = _settings.ToJson();
            return json;
        }
        private bool _openPopup;
        public bool ShouldRenderPopup()
        {
            return _openPopup;
        }

        private ParticleSettings _settings = new();
        private bool _preview_temp;

        public void RenderPopup()
        {
            if (ImGui.Begin("Particle Editor", ref _openPopup, ImGuiWindowFlags.NoCollapse))
            {
                if (ImGui.BeginTable("##particleSettingsTable", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.Resizable))
                {
                    ImGui.TableSetupColumn("Property");
                    ImGui.TableSetupColumn("Value");
                    ImGui.TableHeadersRow();

                    RenderField("Max Particles:", ref _settings.MaxParticles, ref _settings.MaxParticles_temp, 0, 5000);
                    RenderField("Lifetime (Avg):", ref _settings.Lifetime, ref _settings.Lifetime_temp, 0.1f, 10.0f);
                    RenderField("Lifetime Variance:", ref _settings.LifetimeVar, ref _settings.LifetimeVar_temp, 0.0f, 5.0f);
                    RenderField("Emission Rate:", ref _settings.Emission, ref _settings.Emission_temp, 0, 1000);

                    ImGui.SeparatorText("Position & Velocity");
                    RenderField("Angle (XY):", ref _settings.Angle, ref _settings.Angle_temp, -360, 360);
                    RenderField("Angle (Z):", ref _settings.Angle3D, ref _settings.Angle3D_temp, -90, 90);
                    RenderField("Speed (Avg):", ref _settings.Speed, ref _settings.Speed_temp, 0, 100);

                    RenderField("Pos. Variance X:", ref _settings.PosVarX, ref _settings.PosVarX_temp, 0, 20);
                    RenderField("Pos. Variance Y:", ref _settings.PosVarY, ref _settings.PosVarY_temp, 0, 20);
                    RenderField("Pos. Variance Z:", ref _settings.PosVarZ, ref _settings.PosVarZ_temp, 0, 20);

                    RenderField("Vel. Variance X:", ref _settings.VelVarX, ref _settings.VelVarX_temp, 0, 10);
                    RenderField("Vel. Variance Y:", ref _settings.VelVarY, ref _settings.VelVarY_temp, 0, 10);
                    RenderField("Vel. Variance Z:", ref _settings.VelVarZ, ref _settings.VelVarZ_temp, 0, 10);


                    ImGui.SeparatorText("Forces");
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    ImGui.Text("Gravity:");
                    ImGui.TableSetColumnIndex(1);
                    ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X / 3f - ImGui.GetStyle().ItemSpacing.X);
                    ImGui.DragFloat("X##gravityx", ref _settings.GravityX_temp, 0.1f, -100, 100);
                    ImGui.SameLine();
                    ImGui.DragFloat("Y##gravityy", ref _settings.GravityY_temp, 0.1f, -100, 100);
                    ImGui.SameLine();
                    ImGui.DragFloat("Z##gravityz", ref _settings.GravityZ_temp, 0.1f, -100, 100);
                    ImGui.PopItemWidth();
                    ApplyIfChanged(ref _settings.GravityX, ref _settings.GravityX_temp);
                    ApplyIfChanged(ref _settings.GravityY, ref _settings.GravityY_temp);
                    ApplyIfChanged(ref _settings.GravityZ, ref _settings.GravityZ_temp);


                    RenderField("Radial Accel.:", ref _settings.AccelRad, ref _settings.AccelRad_temp, -50, 50);
                    RenderField("Tangential Accel.:", ref _settings.AccelTan, ref _settings.AccelTan_temp, -50, 50);
                    RenderField("Drag:", ref _settings.Drag, ref _settings.Drag_temp, 0.0f, 1.0f);


                    ImGui.SeparatorText("Appearance");
                    RenderField("Size (Start Avg):", ref _settings.SizeStart, ref _settings.SizeStart_temp, 0.01f, 5.0f);
                    RenderField("Size (End Avg):", ref _settings.SizeEnd, ref _settings.SizeEnd_temp, 0.01f, 5.0f);
                    RenderField("Size Variance:", ref _settings.SizeVar, ref _settings.SizeVar_temp, 0.0f, 2.0f);

                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    ImGui.Text("Color (Start):");
                    ImGui.TableSetColumnIndex(1);


                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    ImGui.Text("Color (End):");
                    ImGui.TableSetColumnIndex(1);


                    ImGui.EndTable();
                }

                ImGui.End();
            }
        }

        private void RenderField(string label, ref int value, ref int temp, int min, int max)
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.Text(label);
            ImGui.TableSetColumnIndex(1);
            ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X);
            if (ImGui.DragInt($"##{label}", ref temp, 1.0f, min, max))
            {
                ApplyIfChanged(ref value, ref temp);
            }
            ImGui.PopItemWidth();
        }

        private void RenderField(string label, ref float value, ref float temp, float min, float max)
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.Text(label);
            ImGui.TableSetColumnIndex(1);
            ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X);
            if (ImGui.DragFloat($"##{label}", ref temp, (max - min) / 200f, min, max, "%.2f"))
            {
                ApplyIfChanged(ref value, ref temp);
            }
            ImGui.PopItemWidth();
        }

        private void ApplyIfChanged<T>(ref T field, ref T temp) where T : struct, IEquatable<T>
        {
            if (!field.Equals(temp))
            {
                field = temp;
            }
        }

        public PopupSettings GetPopupSettings()
        {
            if (Owner != null!)
                return new() { Width = 500, Height = 700, Title = $"Particle Editor (0x{Owner.Id:x}, {Owner.Name})" };
            else
                return new PopupSettings();
        }

        private class ParticleSettings
        {
            public int MaxParticles = 1000; public int MaxParticles_temp = 1000;
            public float Lifetime = 2.0f; public float Lifetime_temp = 2.0f;
            public float LifetimeVar = 0.5f; public float LifetimeVar_temp = 0.5f;
            public float Emission = 50; public float Emission_temp = 50;

            public float Angle = -90; public float Angle_temp = -90;
            public float Angle3D = 0; public float Angle3D_temp = 0;
            public float Speed = 20; public float Speed_temp = 20;

            public float PosVarX = 0; public float PosVarX_temp = 0;
            public float PosVarY = 0; public float PosVarY_temp = 0;
            public float PosVarZ = 0; public float PosVarZ_temp = 0;

            public float VelVarX = 0; public float VelVarX_temp = 0;
            public float VelVarY = 0; public float VelVarY_temp = 0;
            public float VelVarZ = 0; public float VelVarZ_temp = 0;

            public float GravityX = 0; public float GravityX_temp = 0;
            public float GravityY = -9.8f; public float GravityY_temp = -9.8f;
            public float GravityZ = 0; public float GravityZ_temp = 0;

            public float AccelRad = 0; public float AccelRad_temp = 0;
            public float AccelTan = 0; public float AccelTan_temp = 0;
            public float Drag = 0.1f; public float Drag_temp = 0.1f;

            public float SizeStart = 1.0f; public float SizeStart_temp = 1.0f;
            public float SizeEnd = 0.1f; public float SizeEnd_temp = 0.1f;
            public float SizeVar = 0.0f; public float SizeVar_temp = 0.0f;

            public Color4 ColorStart = Color4.Orange;
            public Color4 ColorEnd = Color4.Red;


            public JsonObject ToJson()
            {
                var json = new JsonObject
                {
                    ["MaxParticles"] = MaxParticles,
                    ["Lifetime"] = Lifetime,
                    ["LifetimeVar"] = LifetimeVar,
                    ["Emission"] = Emission,
                    ["Angle"] = Angle,
                    ["Angle3D"] = Angle3D,
                    ["Speed"] = Speed,
                    ["PosVarX"] = PosVarX,
                    ["PosVarY"] = PosVarY,
                    ["PosVarZ"] = PosVarZ,
                    ["VelVarX"] = VelVarX,
                    ["VelVarY"] = VelVarY,
                    ["VelVarZ"] = VelVarZ,
                    ["GravityX"] = GravityX,
                    ["GravityY"] = GravityY,
                    ["GravityZ"] = GravityZ,
                    ["AccelRad"] = AccelRad,
                    ["AccelTan"] = AccelTan,
                    ["Drag"] = Drag,
                    ["SizeStart"] = SizeStart,
                    ["SizeEnd"] = SizeEnd,
                    ["SizeVar"] = SizeVar,
                    ["ColorStartR"] = ColorStart.R,
                    ["ColorStartG"] = ColorStart.G,
                    ["ColorStartB"] = ColorStart.B,
                    ["ColorStartA"] = ColorStart.A,
                    ["ColorEndR"] = ColorEnd.R,
                    ["ColorEndG"] = ColorEnd.G,
                    ["ColorEndB"] = ColorEnd.B,
                    ["ColorEndA"] = ColorEnd.A
                };
                return json;
            }

            public void FromJson(JsonObject data)
            {
                if (data == null)
                    return;
                MaxParticles = data["MaxParticles"]?.GetValue<int>() ?? MaxParticles;
                Lifetime = data["Lifetime"]?.GetValue<float>() ?? Lifetime;
                LifetimeVar = data["LifetimeVar"]?.GetValue<float>() ?? LifetimeVar;
                Emission = data["Emission"]?.GetValue<float>() ?? Emission;
                Angle = data["Angle"]?.GetValue<float>() ?? Angle;
                Angle3D = data["Angle3D"]?.GetValue<float>() ?? Angle3D;
                Speed = data["Speed"]?.GetValue<float>() ?? Speed;
                PosVarX = data["PosVarX"]?.GetValue<float>() ?? PosVarX;
                PosVarY = data["PosVarY"]?.GetValue<float>() ?? PosVarY;
                PosVarZ = data["PosVarZ"]?.GetValue<float>() ?? PosVarZ;
                VelVarX = data["VelVarX"]?.GetValue<float>() ?? VelVarX;
                VelVarY = data["VelVarY"]?.GetValue<float>() ?? VelVarY;
                VelVarZ = data["VelVarZ"]?.GetValue<float>() ?? VelVarZ;
                GravityX = data["GravityX"]?.GetValue<float>() ?? GravityX;
                GravityY = data["GravityY"]?.GetValue<float>() ?? GravityY;
                GravityZ = data["GravityZ"]?.GetValue<float>() ?? GravityZ;
                AccelRad = data["AccelRad"]?.GetValue<float>() ?? AccelRad;
                AccelTan = data["AccelTan"]?.GetValue<float>() ?? AccelTan;
                Drag = data["Drag"]?.GetValue<float>() ?? Drag;
                SizeStart = data["SizeStart"]?.GetValue<float>() ?? SizeStart;
                SizeEnd = data["SizeEnd"]?.GetValue<float>() ?? SizeEnd;
                SizeVar = data["SizeVar"]?.GetValue<float>() ?? SizeVar;

                ColorStart = new Color4(
                    data["ColorStartR"]?.GetValue<float>() ?? ColorStart.R,
                    data["ColorStartG"]?.GetValue<float>() ?? ColorStart.G,
                    data["ColorStartB"]?.GetValue<float>() ?? ColorStart.B,
                    data["ColorStartA"]?.GetValue<float>() ?? ColorStart.A
                );
                ColorEnd = new Color4(
                    data["ColorEndR"]?.GetValue<float>() ?? ColorEnd.R,
                    data["ColorEndG"]?.GetValue<float>() ?? ColorEnd.G,
                    data["ColorEndB"]?.GetValue<float>() ?? ColorEnd.B,
                    data["ColorEndA"]?.GetValue<float>() ?? ColorEnd.A
                );

                SyncTemps();
            }

            private void SyncTemps()
            {
                MaxParticles_temp = MaxParticles;
                Lifetime_temp = Lifetime;
                LifetimeVar_temp = LifetimeVar;
                Emission_temp = Emission;
                Angle_temp = Angle;
                Angle3D_temp = Angle3D;
                Speed_temp = Speed;
                PosVarX_temp = PosVarX;
                PosVarY_temp = PosVarY;
                PosVarZ_temp = PosVarZ;
                VelVarX_temp = VelVarX;
                VelVarY_temp = VelVarY;
                VelVarZ_temp = VelVarZ;
                GravityX_temp = GravityX;
                GravityY_temp = GravityY;
                GravityZ_temp = GravityZ;
                AccelRad_temp = AccelRad;
                AccelTan_temp = AccelTan;
                Drag_temp = Drag;
                SizeStart_temp = SizeStart;
                SizeEnd_temp = SizeEnd;
                SizeVar_temp = SizeVar;
            }
        }

        private struct ParticleInstance
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
            public float InitialSize;
            public Color4 Color;
        }

        /// <inheritdoc />
        public void EditorUpdate(FrameEventArgs args)
        {
            if (SceneEditor.PreviewParticles)
                Update(args);
        }

        /// <inheritdoc />
        public void EditorRender(FrameEventArgs args) => Render(args);
    }
}