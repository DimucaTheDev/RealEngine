using RE.Core.Assets;
using RE.Rendering.Lighting;

namespace RE.Rendering.Model;

public class ModelData : DynamicAsset, ICloneable
{
    public required ModelMesh Mesh { get; set; }
    public required Material Material { get; set; }

    public object Clone()
    {
        return new ModelData()
        {
            Mesh = (ModelMesh)Mesh.Clone(),
            Material = (Material)Material.Clone(),
            AssetPath = AssetPath
        };
    }
}