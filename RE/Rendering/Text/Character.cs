using OpenTK.Mathematics;

namespace RE.Rendering.Text;

public struct Character
{
    public int TextureID { get; set; }
    public Vector2 Size { get; set; }
    public Vector2 Bearing { get; set; }
    public int Advance { get; set; }
}