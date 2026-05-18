using RE.Core.Assets;
using RE.Rendering.Lighting;

namespace RE.Rendering.Model;

public class ModelData : DynamicAsset
{
    public required ModelMesh Mesh { get; set; }
    public required Material Material { get; set; } 
}