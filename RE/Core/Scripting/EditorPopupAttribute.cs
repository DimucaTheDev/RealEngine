namespace RE.Core.Scripting
{
    /// <summary>
    /// Specifies that a method should be exposed as a popup dialog in the editor.
    /// </summary>
    /// <param name="id"></param>
    [AttributeUsage(AttributeTargets.Method)]
    public class EditorPopupAttribute(string id) : Attribute
    {
        public string Id { get; } = id;
        public bool Modal { get; set; } = false;
        public int Width { get; set; } = 400;
        public int Height { get; set; } = 300;
    }
}
