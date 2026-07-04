using OpenTK.Windowing.Common;

namespace RE.Editor
{
    /// <summary>
    /// Components can use this interface to render in the scene editor.
    /// </summary>
    public interface IEditorRender
    {
        void EditorRender(double delta);
    }
}
