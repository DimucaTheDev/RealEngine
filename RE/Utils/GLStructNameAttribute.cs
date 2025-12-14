namespace RE.Utils
{
    [AttributeUsage(AttributeTargets.Struct)]
    public class GlStructNameAttribute(string name) : Attribute
    {
        public string StructureName { get; } = name;
    }
}
