using RE.Utils;
using sampler2D = int;

namespace RE.Rendering.Lighting
{
    [GlStructName("Material")]
    public struct MaterialData()
    {
        [GlPropertyName("diffuse")] public sampler2D DiffuseTexture { get; set; } = 0;

        [GlPropertyName("specular")]
        public sampler2D SpecularTexture { get; set; }

        [GlPropertyName("shininess")]
        public float Shininess { get; set; } = 32;
    }
}