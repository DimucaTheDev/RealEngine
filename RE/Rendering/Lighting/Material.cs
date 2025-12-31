using RE.Utils;
using sampler2D = int;

namespace RE.Rendering.Lightning
{
    [GlStructName("Material")]
    public struct Material()
    {
        [GlPropertyName("diffuse")] 
        public sampler2D DiffuseTexture { get; set; } // sampler2D

        [GlPropertyName("specular")]
        public sampler2D SpecularTexture { get; set; }

        [GlPropertyName("shininess")]
        public float Shininess { get; set; } = 32;
    }
}
