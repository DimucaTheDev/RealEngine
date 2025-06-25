using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using RE.Debug;
using RE.Debug.Overlay;
using RE.Rendering;
using RE.Rendering.Camera;
using RE.Rendering.Skybox;
using RE.Rendering.Text;
using System.Drawing;
using System.Runtime.InteropServices;

namespace RE.Core
{
    internal class Game : GameWindow
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr LoadLibrary(string lpFileName);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern void FreeLibrary(nint handle);


        private Game(GameWindowSettings gws, NativeWindowSettings nws) : base(gws, nws) { }
        private Dictionary<nint, string> _loadedLibs = new();

        public static Game Instance { get; private set; }

        FreeTypeFont _font;

        public static void Start()
        {
            using var game = new Game(
                new GameWindowSettings { UpdateFrequency = 60 },
                new NativeWindowSettings { Title = "DaRealEngin", ClientSize = new OpenTK.Mathematics.Vector2i(800, 600) });
            Instance = game;
            Thread.CurrentThread.Name = "Render Thread";
            game.Run();
        }

        protected override void OnLoad()
        {
            foreach (var lib in Directory.GetFiles("Dll", "*.dll"))
            {
                nint handle;
                Console.Write($"Loading DLL \"{lib}\"... ");
                _loadedLibs.Add(handle = LoadLibrary(lib), lib);
                Console.WriteLine($"0x{handle:X}");
            }

            this.UpdateFrame += Time.Update;

            RenderLayerManager.Init();
            Time.Init();
            Camera.Init();
            TextRenderer.Init();
            DebugOverlay.Init();
            SkyboxRenderer.Init();
            new LineRenderable().Init();

            renderable = new Text("123", new(20, 40), new FreeTypeFont(32, "c:/windows/fonts/arial.ttf"), new Vector4(1));
            RenderLayerManager.AddRenderable(renderable, typeof(Text));

            base.OnLoad();
        }

        public Text renderable;

        protected override void OnResize(ResizeEventArgs e)
        {
            Camera.Instance.AspectRatio = (float)e.Width / e.Height;
            GL.Viewport(0, 0, e.Width, e.Height);
            base.OnResize(e);
        }

        protected override void OnRenderFrame(FrameEventArgs args)
        {
            GL.Viewport(0, 0, this.ClientSize.X, this.ClientSize.Y);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            GL.DepthFunc(DepthFunction.Lequal);
            GL.ClearColor(Color.CadetBlue);


            RenderLayerManager.RenderAll(args);


            base.OnRenderFrame(args);

            SwapBuffers();
        }

        protected override void OnUnload()
        {
            base.OnUnload();
            foreach (var lib in _loadedLibs)
            {
                Console.WriteLine($"Unloading library \"{lib.Value}\"");
                FreeLibrary(lib.Key);
            }
        }
    }
}
