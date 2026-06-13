using JetBrains.Annotations;

namespace RE.Core.Scripting.Attributes;

[AttributeUsage(AttributeTargets.Method)]
[MeansImplicitUse(ImplicitUseTargetFlags.Itself)]
public class ConsoleCommandAttribute : Attribute
{
    public required string Name { get; init; }
    public string? Description { get; init; }
}