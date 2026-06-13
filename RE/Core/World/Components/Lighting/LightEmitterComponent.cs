using System.Text.Json.Nodes;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core.Scripting.Attributes;
using RE.Core.Ui.Debug;
using RE.Editor;
using RE.Rendering.Lighting;
using RE.Rendering.Renderables;

namespace RE.Core.World.Components.Lighting
{
    /// <summary>
    /// wip
    /// </summary>
    public enum LightType
    {
        Directional,
        Point,
        Spot,
    }

    /// <summary>
    /// wip
    /// </summary>
    public class LightEmitterComponent : Component, IEditorRender
    {
        [EditorProperty] public LightType LightType { get; set; }

        [EditorProperty]
        public Vector3 AmbientColor
        {
            get;
            set
            {
                field = value;
                UpdateLight();
            }
        } = Vector3.Zero;

        [EditorProperty]
        public Vector3 DiffuseColor
        {
            get;
            set
            {
                field = value;
                UpdateLight();
            }
        } = Vector3.One;

        [EditorProperty]
        public Vector3 SpecularColor
        {
            get;
            set
            {
                field = value;
                UpdateLight();
            }
        } = Vector3.One;

        [EditorProperty, IfNot(nameof(LightType), LightType.Directional), ValueLimit(Min = 0f, Max = 1f)]
        public float Constant
        {
            get;
            set
            {
                field = value;
                UpdateLight();
            }
        } = 1.0f;

        [EditorProperty, IfNot(nameof(LightType), LightType.Directional), ValueLimit(Min = 0f, Max = 1f, Step = 0.01f)]
        public float Linear
        {
            get;
            set
            {
                field = value;
                UpdateLight();
            }
        } = 0.09f;

        [EditorProperty, IfNot(nameof(LightType), LightType.Directional), ValueLimit(Min = 0, Max = 1f, Step = 0.001f)]
        public float Quadratic
        {
            get;
            set
            {
                field = value;
                UpdateLight();
            }
        } = 0.032f;

        [EditorProperty, If(nameof(LightType), LightType.Spot), ValueLimit(Min = 0, Max = 90, Step = 0.1f /* 180? */)]
        public float CutOff
        {
            get;
            set
            {
                field = value;
                UpdateLight();
            }
        } = 12.5f;

        [EditorProperty, If(nameof(LightType), LightType.Spot), ValueLimit(Min = 0f, Max = 90, Step = 0.1f /* 180? */)]
        public float OuterCutOff
        {
            get;
            set
            {
                field = value;
                UpdateLight();
            }
        } = 17.5f;

        private SpriteRenderer _bulbSprite = new(Vector3.Zero, "assets/editor/sprites/bulb.png", scale: 0.3f);
        private ILightSource _lightSource = null!;

        // we should rethink how we call this.... maybe
        private void UpdateLight()
        {
            Owner.Scene.LightSources.Remove(_lightSource);
            Vector3 v = new(0, -1, 0);
            Quaternion q = Owner.Transform.Rotation;
            Vector3 qv = new(q.X, q.Y, q.Z);
            Vector3 t = 2f * Vector3.Cross(qv, v);
            Vector3 result = v + q.W * t + Vector3.Cross(qv, t);

            switch (LightType)
            {
                case LightType.Directional:

                    _lightSource = new DirectionalLight
                    {
                        AmbientColor = AmbientColor,
                        DiffuseColor = DiffuseColor,
                        Direction = result,
                        SpecularColor = SpecularColor
                    };
                    break;
                case LightType.Point:
                    _lightSource = new PointLight
                    {
                        AmbientColor = AmbientColor,
                        SpecularColor = SpecularColor,
                        DiffuseColor = DiffuseColor,
                        Constant = Constant,
                        Linear = Linear,
                        Position = Owner.Transform.Position,
                        Quadratic = Quadratic,
                    };
                    break;
                case LightType.Spot:

                    _lightSource = new SpotLight
                    {
                        AmbientColor = AmbientColor,
                        DiffuseColor = DiffuseColor,
                        Constant = Constant,
                        CutOff = MathF.Cos(MathHelper.DegreesToRadians(CutOff)),
                        Direction = result,
                        Linear = Linear,
                        OuterCutOff = MathF.Cos(MathHelper.DegreesToRadians(OuterCutOff)),
                        Position = Owner.Transform.Position,
                        Quadratic = Quadratic,
                        SpecularColor = SpecularColor
                    };
                    break;
            }

            Owner.Scene.LightSources.Add(_lightSource);
        }

        public override void Update(FrameEventArgs args)
        {
            UpdateLight();
        }

        public override void Destroy()
        {
            Owner.Scene.LightSources.Remove(_lightSource);
        }
 
        public void EditorRender(FrameEventArgs args)
        {
            UpdateLight();
            _bulbSprite.Position = Owner.Transform.Position;
            _bulbSprite.Render(args);
            switch (LightType)
            {
                case LightType.Spot:

                    Vector3 v = new(0, -1, 0);
                    Quaternion q = Owner.Transform.Rotation;
                    Vector3 qv = new(q.X, q.Y, q.Z);
                    Vector3 t = 2f * Vector3.Cross(qv, v);
                    Vector3 result = v + q.W * t + Vector3.Cross(qv, t);

                    const int lines = 16;
                    Vector3 forward = Vector3.Normalize(result);
                    Vector3 up = Vector3.UnitY;
                    if (Math.Abs(Vector3.Dot(forward, up)) > 0.99f)
                        up = Vector3.UnitX;

                    Vector3 right = Vector3.Normalize(Vector3.Cross(forward, up));
                    up = Vector3.Normalize(Vector3.Cross(right, forward));

                    float angle = MathF.Acos(MathF.Cos(MathHelper.DegreesToRadians(OuterCutOff)));
                    float radius = MathF.Tan(angle);

                    Vector3[] ringPoints = new Vector3[lines];

                    for (int i = 0; i < lines; i++)
                    {
                        float theta = i * MathF.PI / (lines / 2);

                        Vector3 offset = (MathF.Cos(theta) * right + MathF.Sin(theta) * up) * radius;
                        Vector3 dir = Vector3.Normalize(forward + offset);
                        Vector3 end = Owner.Transform.Position + dir * 4;

                        ringPoints[i] = end;

                        if (i % 2 == 0)
                            continue;

                        LineRenderer.DrawLine(Owner.Transform.Position, end,
                           (1, 0, 0, 1),
                           (0, 1, 0, 1));
                    }

                    for (int i = 0; i < lines; i++)
                    {
                        Vector3 a = ringPoints[i];
                        Vector3 b = ringPoints[(i + 1) % lines];

                        LineRenderer.DrawLine(a, b,
                            (0, 1, 0, 1),
                            (0, 1, 0, 1));
                    }


                    break;
            }
        }
    }
}
