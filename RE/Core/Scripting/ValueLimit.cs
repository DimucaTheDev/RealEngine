namespace RE.Core.Scripting
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class ValueLimit : Attribute
    {
        public double Min { get; set; } = double.MinValue;
        public double Max { get; set; } = double.MaxValue;
    }
}
