using System.Reflection;
using Hexa.NET.ImGui;

namespace RE.Debug.Overlay.Editor.PropertyDrawers
{
    internal class IntDrawer : IPropertyDrawer
    {
        public bool Draw(string label, ref object value, PropertyInfo propInfo)
        {
            int intValue = (int)value;
            bool changed = false;

            changed = ImGui.InputInt(label, ref intValue);

            if (changed)
            {
                value = intValue;
            }

            return changed;
        }
    }
}
