namespace RE.Core.Scripting
{
    [AttributeUsage(AttributeTargets.Method)]
    internal class EditorPopup(string id) : Attribute
    {
        public string Id { get; } = id;
        public bool Modal { get; set; } = false;
        public int Width { get; set; } = 400;
        public int Height { get; set; } = 300;
    }
}
