namespace RE.Core.Scripting.Attributes
{
    /// <summary>
    /// Specifies that a class requires the presence of a component of the specified type.
    /// </summary>
    /// <remarks>Apply this attribute to a class to indicate a dependency on another component type. Multiple
    /// instances of this attribute can be applied to declare multiple required components.</remarks>
    /// <param name="type">The type of component that must be present for the attributed class. Cannot be null.</param>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    internal class RequiresComponentAttribute(Type type) : Attribute
    {
        public Type RequiredComponent { get; } = type;
    }
}
