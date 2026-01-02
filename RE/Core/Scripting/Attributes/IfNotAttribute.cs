namespace RE.Core.Scripting.Attributes
{
    /// <summary>
    /// Specifies a conditional requirement for a property based on the value of another property. When applied, the
    /// target property is considered relevant only if the specified property does not have the given value.
    /// </summary>
    /// <remarks>Apply this attribute to a property to indicate that it should be considered only when another
    /// property's value does not match the specified value. This is commonly used in validation or serialization
    /// scenarios to control property relevance based on object state.</remarks>
    /// <param name="v">The name of the property whose value is evaluated for the condition. Cannot be null or empty.</param>
    /// <param name="val">The value to compare against the specified property. If the property's value does not equal this value, the
    /// condition is met.</param>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public sealed class IfNotAttribute(string v, object? val) : Attribute
    {
        public string Name { get; } = v;
        public object? Value { get; } = val;
    }
}
