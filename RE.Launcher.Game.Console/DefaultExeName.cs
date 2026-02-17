namespace RE.Launcher.Game.Console;

[AttributeUsage(AttributeTargets.Assembly)]
internal class DefaultExeName(string value) : Attribute
{
    public readonly string ExeName = value;
}