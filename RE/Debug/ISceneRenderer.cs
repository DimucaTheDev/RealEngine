using OpenTK.Windowing.Common;

namespace RE.Debug
{
    public interface ISceneRenderer // show something in scene editor. i dunno how i call it
    {
        void DebugRender(FrameEventArgs args);
    }
}
