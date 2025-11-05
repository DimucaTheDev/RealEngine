using RE.Core.Assets;

namespace RE.Rendering.Lightning;

public interface ILightSource
{
    void SetParams(ShaderProgram program);
}