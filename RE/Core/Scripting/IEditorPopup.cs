namespace RE.Core.Scripting
{
    // todo: add XML docs
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
