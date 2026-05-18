using System.Diagnostics;
using JetBrains.Annotations;
using RE.Launchers;

namespace RE.Utils
{
    [Obsolete("This class is only used for debugging purposes and should not be used in production code.", error: true)]
    internal class DebugEntryPoint
    {
        [Conditional("DEBUG")]
        private static void Main(string[] args) => GameLauncher.Run(args);
    }
}
