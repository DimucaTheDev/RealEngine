namespace RE.Core.Scripting
{
    [AttributeUsage(AttributeTargets.Method)]
    internal class EditorButtonAttribute : Attribute
    {
        public string? ShownText { get; set; }
    }
}
