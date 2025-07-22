namespace RE.Core.Scripting
{
    [AttributeUsage(AttributeTargets.Class)]
    internal class ComponentInfo(string group) : Attribute
    {
        public string Group { get; } = group;
        public string? Description { get; set; }
    }
}
