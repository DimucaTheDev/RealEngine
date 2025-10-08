namespace RE.Core.Scripting
{
    //todo: parse this in SceneEditor
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    internal class RequiresComponentAttribute(Type type) : Attribute
    {
        public Type RequiredComponent { get; init; } = type;
    }
}
