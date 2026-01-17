using Hexa.NET.ImGui;

namespace RE.Editor.Notification
{
    public class ToastManagerConfig
    {
        public const float PaddingX = 20.0f;
        public const float PaddingY = 20.0f;
        public const float PaddingMessageY = 10.0f;
        public const float FadeInOutTime = 0.150f; 
        public const float Opacity = 1.0f;
        public const bool UseSeparator = true;
        public const ImGuiWindowFlags ToastFlags =
            ImGuiWindowFlags.AlwaysAutoResize |
            ImGuiWindowFlags.NoDecoration |
            ImGuiWindowFlags.NoInputs |
            ImGuiWindowFlags.NoNav |
            ImGuiWindowFlags.NoSavedSettings;
    }
}