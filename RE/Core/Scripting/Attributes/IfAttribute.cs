namespace RE.Core.Scripting.Attributes
{
    /// <summary>
    /// Specifies a conditional requirement for a property based on the value of another property. Apply this attribute
    /// to a property to indicate that it is relevant only when a specified property has a particular value.
    /// </summary>
    /// <remarks>Multiple instances of this attribute can be applied to a property to define complex
    /// conditional logic. This attribute is typically used in validation or UI scenarios to control property visibility
    /// or requirement based on other property values.</remarks>
    /// <param name="propertyName">The name of the property whose value determines whether the attributed property is applicable.</param>
    /// <param name="propertyValue">The value to compare against the specified property. The attributed property is considered relevant when the
    /// named property's value equals this value.</param>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public sealed class IfAttribute(string propertyName, object? propertyValue) : Attribute
    {
        public string Name { get; } = propertyName;
        public object? Value { get; } = propertyValue;
    }
}
