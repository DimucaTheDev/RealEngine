using System.Reflection;
using Hexa.NET.ImGui;

namespace RE.Debug.Overlay.Editor.PropertyDrawers
{
    internal class BoolDrawer : IPropertyDrawer
    {
        public bool Draw(string label, ref object value, PropertyInfo propInfo)
        {
            bool boolValue = (bool)value;
            bool changed = false;

            changed = ImGui.Checkbox(label, ref boolValue);

            if (changed)
            {
                value = boolValue;
            }
            return changed;
        }
    }
}
