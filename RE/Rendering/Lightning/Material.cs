using RE.Utils;

namespace RE.Rendering.Lightning
{
    [GlStructName("Material")]
    public struct Material()
    {
        [GlPropertyName("diffuse")]
        public int DiffuseTexture { get; set; } // sampler2D

        [GlPropertyName("specular")]
        public int SpecularTexture { get; set; }

        [GlPropertyName("shininess")] 
        public float Shininess { get; set; } = 32;
    }
}
