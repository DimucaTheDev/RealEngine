namespace RE.Core.Scripting
{
    // why the hell does this exist 😿
    public interface IEditorPopup
    {
        bool ShouldRenderPopup();
        void RenderPopup();
        PopupSettings GetPopupSettings();
    }

    public struct PopupSettings
    {
        public string Title;
        public int Width;
        public int Height;
    }
}
