using System.Reflection;
using Hexa.NET.ImGui;

namespace RE.Debug.Overlay.Editor.PropertyDrawers
{
    internal class MeshComponentDrawer : IPropertyDrawer
    {
        public bool Draw(string label, ref object value, PropertyInfo propInfo)
        {
            ImGui.Text(value.ToString());

            return false;
        }
    }
}
