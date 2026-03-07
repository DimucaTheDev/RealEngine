using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using Hexa.NET.ImGui.Backends.Vulkan;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using RE.Audio;
using RE.Core;
using RE.Core.Assets;
using RE.Core.Assets.Providers;
using RE.Core.Initializing;
using RE.Core.PluginSystem;
using RE.Core.Scripting;
using RE.Core.World;
using RE.Core.World.Physics;
using RE.Debug;
using RE.Debug.Overlay;
using RE.Editor;
using RE.Rendering;
using RE.Utils;
using RenderdocSharp;
using Serilog;

namespace RE.Launchers
{
    public class EditorLauncher
    {
        public static bool Invoked { get; private set; }
        public static void Run(string[] args)
        {
            throw new NotImplementedException(); 
        }
    }
}
