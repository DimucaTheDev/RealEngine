using OpenTK.Mathematics;
using RE.Core.Assets;
using RE.Utils;

namespace RE.Rendering.Lightning
{
    [GlStructName(StructName)]
    public struct DirectionalLight : ILightSource
    {
        const string StructName = "dirLight";

        [GlPropertyName("direction")]
        public Vector3 Direction { get; set; }

        [GlPropertyName("ambient")]
        public Vector3 AmbientColor { get; set; }

        [GlPropertyName("diffuse")]
        public Vector3 DiffuseColor { get; set; }

        [GlPropertyName("specular")]
        public Vector3 SpecularColor { get; set; }

        public void SetParams(ShaderProgram program)
        {
            program.SetStruct(StructName, this);
        }
    }
}
