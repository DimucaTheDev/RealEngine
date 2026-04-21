using System.Collections;
using DotRecast.Core.Collections.Extensions;

namespace RE.Core.Ui.Controls
{
    internal class UiControlList(UiControl owner) : IEnumerable<UiControl>
    {
        private readonly List<UiControl> _controls = new();

        public UiControl this[int index] => _controls[index];

        public void Add(UiControl control)
        {
            if (control.Parent != null)
                throw new InvalidOperationException("Control already has a parent.");
            control.Parent = owner;
            _controls.Add(control);
            ResortControls();
        }

        public void AddRange(params IEnumerable<UiControl> controls)
        {
            var uiControls = controls as UiControl[] ?? controls.ToArray();
            if (uiControls.Any(c => c.Parent != null))
                throw new InvalidOperationException("Control already has a parent.");

            uiControls.ForEach(c => c.Parent = owner);
            _controls.AddRange(uiControls);
            ResortControls();
        }

        public void Remove(UiControl control)
        {
            control.Parent = null;
            _controls.Remove(control);
        }

        public void Clear()
        {
            _controls.ForEach(c => c.Parent = null);
            _controls.Clear();
        }

        internal void ResortControls()
        {
            _controls.Sort((c1, c2) => -c1.ZIndex.CompareTo(c2.ZIndex));
        }

        public IEnumerator<UiControl> GetEnumerator() => _controls.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
