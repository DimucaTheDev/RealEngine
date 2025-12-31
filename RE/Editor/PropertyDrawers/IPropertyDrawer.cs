using System.Reflection;

namespace RE.Editor.PropertyDrawers
{
    internal interface IPropertyDrawer
    {
        bool Draw(string label, ref object value, PropertyInfo propInfo);
    }
}
