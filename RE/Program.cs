using OpenTK.Windowing.Desktop;
using RE.Core;

namespace RE;

internal class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        //new Test(new GameWindowSettings() { UpdateFrequency = 60 }, new NativeWindowSettings() { }).Run();
        //return;
        Game.Start(args);
    }
}