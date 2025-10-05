using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Nodes;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core.Scripting;
using RE.Debug;
using RE.Rendering.Renderables;
using RE.Utils;

namespace RE.Core.World.Components.Lighting
{
    public enum LightType
    {
        Directional,
        Point,
        Spot,
    }
    public class LightEmitterComponent : Component
    {
        [EditorProperty] public LightType LightType { get; set; }

        [EditorProperty, IfNot(nameof(LightType), LightType.Point)]
        public Vector3 Direction { get; set; } = -Vector3.UnitY;

        [EditorProperty] public Vector3 AmbientColor { get; set; } = Vector3.Zero;

        [EditorProperty] public Vector3 DiffuseColor { get; set; } = Vector3.One;

        [EditorProperty] public Vector3 SpecularColor { get; set; } = Vector3.One;

        [EditorProperty, IfNot(nameof(LightType), LightType.Directional)]
        public Vector3 /*maybe add pos type: offset or absolute*/ Position { get; set; } = Vector3.Zero;

        [EditorProperty, IfNot(nameof(LightType), LightType.Directional), ValueLimit(Min = 0.5f, Max = 1)]
        public float Constant { get; set; } = 1.0f;

        [EditorProperty, IfNot(nameof(LightType), LightType.Directional), ValueLimit(Min = 0.001f, Max = 0.3f)]
        public float Linear { get; set; } = 0.09f;

        [EditorProperty, IfNot(nameof(LightType), LightType.Directional), ValueLimit(Min = 0.00001f, Max = 0.1f)]
        public float Quadratic { get; set; } = 0.032f;

        [EditorProperty, If(nameof(LightType), LightType.Spot), ValueLimit(Min = 0.7071f, Max = 0.9962f)]
        public float CutOff { get; set; } = MathF.Cos(MathHelper.DegreesToRadians(12.5f));

        [EditorProperty, If(nameof(LightType), LightType.Spot), ValueLimit(Min = 0.5f, Max = 0.9962f)]
        public float OuterCutOff { get; set; } = MathF.Cos(MathHelper.DegreesToRadians(17.5f));

        private SpriteRenderer _bulbSprite = new(Vector3.Zero, "assets/sprites/editor/bulb.png", scale: 0.5f);

        public override JsonNode GetSaveData()
        {
            return GetDataForProperties();
        }

        public override void Render(FrameEventArgs args)
        {
            //todo: see comment on Position
            _bulbSprite.Position = Owner.Transform.Position + Position;
            _bulbSprite.Render(args);
            switch (LightType)
            {
                case LightType.Spot:
                    Vector3 forward = Vector3.Normalize(Direction);
                    Vector3 up = Vector3.UnitY;
                    if (Math.Abs(Vector3.Dot(forward, up)) > 0.99f)
                        up = Vector3.UnitX;

                    Vector3 right = Vector3.Normalize(Vector3.Cross(forward, up));
                    up = Vector3.Normalize(Vector3.Cross(right, forward));

                    float angle = MathF.Acos(OuterCutOff);
                    float radius = MathF.Tan(angle);

                    Vector3[] ringPoints = new Vector3[8];

                    for (int i = 0; i < 8; i++)
                    {
                        float theta = i * MathF.PI / 4f;

                        Vector3 offset = (MathF.Cos(theta) * right + MathF.Sin(theta) * up) * radius;
                        Vector3 dir = Vector3.Normalize(forward + offset);
                        Vector3 end = Owner.Transform.Position + dir * 4f;

                        ringPoints[i] = end;

                        LineRenderer.DrawLine(Owner.Transform.Position, end,
                           (1, 0, 0, 1),
                           (0, 1, 0, 1));
                    }

                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 a = ringPoints[i];
                        Vector3 b = ringPoints[(i + 1) % 8];

                        LineRenderer.DrawLine(a, b,
                            (0, 1, 0, 1),
                            (0, 1, 0, 1));
                    }


                    break;
                default:
                    break;
            }
        }
    }
}
