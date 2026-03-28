using RE.Core.Assets;

namespace RE.Rendering.Lighting;

public interface ILightSource
{
    void SetParams(ShaderProgram program);
}