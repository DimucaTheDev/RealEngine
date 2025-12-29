using System;
using System.Collections.Generic;
using System.Text;
using OpenTK.Mathematics;

namespace RE.Debug.Overlay.Editor.Panels.Viewport
{
    internal interface IViewportRenderer
    {
        public void ViewportRender(Vector2 pos, Vector2 size);
    }
}
