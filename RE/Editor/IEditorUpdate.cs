using OpenTK.Windowing.Common;

namespace RE.Editor
{
    /// <summary>
    /// Components can use this interface to update its state in the scene editor.
    /// </summary>
    public interface IEditorUpdate
    {
        void EditorUpdate(double delta);
    }
}
