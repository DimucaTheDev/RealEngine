namespace RE.Core.Scripting.Attributes
{
    /// <summary>
    /// Specifies metadata for a component class, including its group and an optional description.
    /// </summary>
    /// <param name="group">The name of the group to which the component belongs. Cannot be null.</param>
    [AttributeUsage(AttributeTargets.Class)]
    public class ComponentInfoAttribute(string group) : Attribute
    {
        public string Group { get; } = group;
        public string? Description { get; set; }
    }
}
