using OpenTK.Mathematics;
using RE.Core.World;
using RE.Core.World.Components.Physics;

namespace RE.Core.Ui.Controls
{
    internal abstract class UiControl
    {
        public UiControl? Parent { get; internal set; }
        public UiControlList Children { get; internal set; }

        public string Name { get; set; }
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
            Name = GetType().Name;
        }

       public virtual void Render() { }
       public virtual void Update(float delta) { }
       public virtual void OnMouseHoverEnter() { }
       public virtual void OnMouseHoverLeave() { }
       public virtual void OnClick() { }

       public virtual (Vector2 Position, Vector2 Scale) GetBoundary()
       {
           return (Position, Scale);
       }
    }
}
