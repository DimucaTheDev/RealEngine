using System;
using System.Collections.Generic;
using System.Text;
using OpenTK.Mathematics;
using RE.Rendering.Renderables;

namespace RE.Core.Ui.Controls
{
    internal class ImageControl : UiControl
    {
        public ImageRenderer ImageRenderer { get; }

        public ImageControl(string path)
        {
            ImageRenderer = new ImageRenderer(path, Vector2.Zero);
            Scale = ImageRenderer.Scale;
        }

        public ImageControl(ImageRenderer imageRenderer)
        {
            ImageRenderer = imageRenderer;
            Scale = ImageRenderer.Scale;
        }

        public override void Update(float delta)
        {
            ImageRenderer.Position = Position;
            ImageRenderer.Scale = Scale;
        }

        public override void Render()
        {
            ImageRenderer.Render(default);
        }
    }
}
