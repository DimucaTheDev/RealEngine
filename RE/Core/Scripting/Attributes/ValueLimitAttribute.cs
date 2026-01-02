namespace RE.Core.Scripting.Attributes
{
    /// <summary>
    /// Specifies minimum and maximum allowed values for a property in editor.
    /// </summary>
    /// <remarks>Apply this attribute to a property to indicate the valid range of values it can accept. This
    /// attribute is typically used for validation or tooling purposes and does not enforce value limits at runtime by
    /// itself.</remarks>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class ValueLimitAttribute : Attribute
    {
        public double Min { get; set; } = double.MinValue;
        public double Max { get; set; } = double.MaxValue;
        public double Step { get; set; } = 0.05;
    }
}
