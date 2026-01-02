namespace RE.Core.Scripting.Attributes
{
    /// <summary>
    /// Adds buttons to the editor for invoking methods.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class EditorButtonAttribute : Attribute
    {
        public string? ShownText { get; set; }
    }
}
