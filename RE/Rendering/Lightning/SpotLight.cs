using System;
using System.Collections.Generic;
using System.Text;
using OpenTK.Mathematics;
using RE.Utils;

namespace RE.Rendering.Lightning
{
    [GlStructName("SpotLight")]
    public struct SpotLight()
    {
        [GlPropertyName("position")]
        public Vector3 Position { get; set; } = Vector3.Zero;

        [GlPropertyName("direction")]
        public Vector3 Direction { get; set; } = Vector3.UnitY;

        [GlPropertyName("cutOff")]
        public float CutOff { get; set; } = 0.0f;

        [GlPropertyName("outerCutOff")]
        public float OuterCutOff { get; set; } = 0.0f;

        [GlPropertyName("constant")]
        public float Constant { get; set; } = 1.0f;

        [GlPropertyName("linear")]
        public float Linear { get; set; } = 0.0f;

        [GlPropertyName("quadratic")]
        public float Quadratic { get; set; } = 0.0f;

        [GlPropertyName("ambient")]
        public Vector3 AmbientColor { get; set; } = Vector3.Zero;

        [GlPropertyName("diffuse")]
        public Vector3 DiffuseColor { get; set; } = Vector3.Zero;

        [GlPropertyName("specular")]
        public Vector3 SpecularColor { get; set; } = Vector3.Zero;
    }
}
