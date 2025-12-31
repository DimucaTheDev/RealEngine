using System.Reflection;
using Hexa.NET.ImGui;

namespace RE.Editor.PropertyDrawers
{
    internal class Vector3Drawer : IPropertyDrawer
    {
        public bool Draw(string label, ref object value, PropertyInfo propInfo)
        {
            ImGui.Text(value.ToString());

            return false;
        }
    }
}
