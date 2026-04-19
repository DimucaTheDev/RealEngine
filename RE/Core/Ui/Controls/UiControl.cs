using System;
using System.Collections.Generic;
using System.Text;
using OpenTK.Mathematics;

namespace RE.Core.Ui.Controls
{
    internal abstract class UiControl
    {
        public UiControl? Parent { get; internal set; }
        public UiControlList Children { get; internal set; }

        public Vector2 Position { get; set; }
        public Vector2 Scale { get; set; }
        public bool Visible { get; set; } = true;
        // used when rendering controls with same parent. Ignored when comparing controls with different parents
        public int ZIndex
        {
            get;
            set
            {
                field = value;
                Parent?.Children.ResortControls();
            }
        }

        protected UiControl()
        {
            Children = new UiControlList(this);
        }

       public virtual void Render() { }
       public virtual void Update(float delta) { }
       public virtual void OnMouseHoverEnter() { }
       public virtual void OnMouseHoverLeave() { }
       public virtual void OnClick() { }
    }
}
