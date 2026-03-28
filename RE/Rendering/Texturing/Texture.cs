using Hexa.NET.ImGui;
using RE.Core.Assets;

namespace RE.Rendering.Texturing
{
    public abstract class Texture : DynamicAsset
    {
        public abstract uint AsOpenGl();
        public abstract ImTextureRef AsImGui();
        public abstract void Delete();

        public static implicit operator ImTextureRef(Texture texture) => texture.AsImGui();
    }
}
