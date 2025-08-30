using System.Text.RegularExpressions;

namespace RE.Utils
{
    [AttributeUsage(AttributeTargets.Assembly)]
    internal class GitCommitAttribute(string? value) : Attribute
    {
        public readonly string? CommitHash = Regex.IsMatch(value ?? "", "^[0-9a-f]{40}$") ? value : null;
    }
}
