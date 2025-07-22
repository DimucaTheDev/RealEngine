namespace RE.Core.Scripting
{
    internal interface IEditorPopup
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
