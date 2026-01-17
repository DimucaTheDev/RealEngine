using System;
using System.Collections.Generic;
using System.Text;
using RE.Core;

namespace RE.Launchers
{
    public class GameLauncher
    {
        public static void Run(string[] args)
        {
            Game.Start(args);
        }
    }
}
