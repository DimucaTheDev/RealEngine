namespace RE.Libs.Grille.ImGuiTK;

public static class ShaderCode
{
    public static string FragmentSource => GetText("Assets/shaders/debug_imgui.frag");
    public static string VertexSource => GetText("Assets/shaders/debug_imgui.vert");

    private static string GetText(string name)
    {
        using var stream = File.OpenRead(name);
        using var reader = new StreamReader(stream, leaveOpen: true);
        return reader.ReadToEnd();
    }
}