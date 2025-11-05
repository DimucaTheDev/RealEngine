using OpenTK.Mathematics;
using RE.Core.Assets;
using RE.Utils;

namespace RE.Rendering.Lightning
{
    [GlStructName(StructName)]
    public struct PointLight() : ILightSource
    {
        const string StructName = "PointLight";

        [GlPropertyName("position")]
        public Vector3 Position { get; set; } = Vector3.Zero;

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

        public void SetParams(ShaderProgram program)
        {
            program.SetStructArray("pointLights", [this]);
        }
    }
}
