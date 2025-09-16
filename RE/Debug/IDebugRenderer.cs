using OpenTK.Windowing.Common;

namespace RE.Debug
{
    /// <summary>
    /// Components can use this interface to render debug information in the scene editor.
    /// </summary>
    public interface IDebugRenderer // show something in scene editor. i dunno how i call it
    {
        void DebugRender(FrameEventArgs args);
    }
}
