using System;
using System.Collections.Generic;
using System.Text;
using OpenTK.Windowing.Common;
using RE.Core.Ui.Controls;
using RE.Core.World;
using Serilog;

namespace RE.Core.Ui
{
    internal static class Hud
    {
        public static CanvasControl Root { get; } = new();

        internal static void Update(FrameEventArgs e)
        {
            // input

            //
            UpdateControl(Root, e);
        }

        internal static void Render()
        {
            RenderControl(Root);
        }

        private static void UpdateControl(UiControl control, FrameEventArgs e)
        {
            control.Update((float)e.Time);
            foreach (var child in control.Children)
                UpdateControl(child, e);
        }

        private static void RenderControl(UiControl control)
        {
            if(!control.Visible) return;

            control.Render();
            foreach (var child in control.Children)
                RenderControl(child);
        }
    }
}
