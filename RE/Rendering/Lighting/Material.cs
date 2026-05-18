using RE.Core.Assets;
using RE.Rendering.Texturing;
using RE.Utils;
using sampler2D = int;

namespace RE.Rendering.Lighting
{
    public class Material
    {
        public Material()
        {
            ShaderProgram = new ShaderProgram();
            ShaderProgram.AttachShader("assets/shaders/assimp.vert");
            ShaderProgram.AttachShader("assets/shaders/assimp.frag");
        }
        
        public ShaderProgram ShaderProgram { get; set; }
        public Texture Texture;
        public MaterialData Data = new();
    }
}