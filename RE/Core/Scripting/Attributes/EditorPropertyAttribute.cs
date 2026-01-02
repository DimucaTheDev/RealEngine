namespace RE.Core.Scripting.Attributes
{
    /// <summary>
    /// Specifies metadata for a property to control how it is displayed and edited in an editor environment.
    /// </summary>
    /// <param name="name">The display name to use for the property in the editor. If null, the property's name is used.</param>
    /// <param name="readonly"><see langword="true"/> to indicate that the property should be read-only in the editor; otherwise, <see langword="false"/>.</param>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class EditorPropertyAttribute(string? name = null, bool @readonly = false) : Attribute
    {
        public string? DisplayedName { get; set; } = name;
        public bool IsReadOnly { get; set; } = @readonly;
    }
}
