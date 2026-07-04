using OpenTK.Windowing.Common;
using RE.Core.Ui.Controls;

namespace RE.Core.Ui
{
    internal static class Hud
    {
        public static CanvasControl Root { get; } = new() { Name = "Canvas" };

        internal static void Update(double e)
        {
            // input

            //
            UpdateControl(Root, e);
        }

        internal static void Render()
        {
            RenderControl(Root);
        }

        private static void UpdateControl(UiControl control, double e)
        {
            control.Update((float)e);
            foreach (var child in control.Children)
                UpdateControl(child, e);
        }

        private static void RenderControl(UiControl control)
        {
            if (!control.Visible)
                return;

            control.Render();
            foreach (var child in control.Children)
                RenderControl(child);
        }
    }
}
