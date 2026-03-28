using System.Diagnostics;
using RE.Launchers;

namespace RE.Utils
{
    [Obsolete("This class is only used for debugging purposes and should not be used in production code.", error: true)]
    internal class DebugEntryPoint
    {
        [Conditional("DEBUG")]
        static void Main(string[] args) => GameLauncher.Run(args);
    }
}
