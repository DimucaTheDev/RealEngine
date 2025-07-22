namespace RE.Core.Scripting
{
    [AttributeUsage(AttributeTargets.Method)]
    internal class EditorButton : Attribute
    {
        public string? ShownText { get; set; }
    }
}
