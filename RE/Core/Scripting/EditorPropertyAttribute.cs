namespace RE.Core.Scripting
{
    /// <summary>
    /// Used to show """user-friendly""" name of property in Scene Editor
    /// </summary>
    /// <param name="name"></param>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class EditorPropertyAttribute(string? name = null, bool @readonly = false) : Attribute
    {
        public string? DisplayedName { get; set; } = name;
        public bool IsReadOnly { get; set; } = @readonly;
    }
}
