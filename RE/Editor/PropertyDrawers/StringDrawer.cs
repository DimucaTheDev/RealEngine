using System.Reflection;
using Hexa.NET.ImGui;

namespace RE.Editor.PropertyDrawers
{
    internal class StringDrawer : IPropertyDrawer
    {
        private const int MaxStringSize = 4096;

        public bool Draw(string label, ref object value, PropertyInfo propInfo)
        {
            string stringValue = (string)value;

            bool changed = ImGui.InputText(label, ref stringValue, MaxStringSize, ImGuiInputTextFlags.EnterReturnsTrue);

            if (changed)
            {
                value = stringValue;
            }

            return changed;
        }
    }
}
