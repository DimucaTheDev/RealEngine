using System.Reflection;
using Hexa.NET.ImGui;

namespace RE.Debug.Overlay.Editor.PropertyDrawers
{
    internal class FloatDrawer : IPropertyDrawer
    {
        public bool Draw(string label, ref object value, PropertyInfo propInfo)
        {
            float floatValue = (float)value;
            bool changed = false;

            changed = ImGui.InputFloat(label, ref floatValue);

            if (changed)
            {
                value = floatValue;
            }

            return changed;
        }
    }
}
