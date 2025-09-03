using OpenTK.Graphics.OpenGL4;
using Log = Serilog.Log;

namespace RE.Core.Assets
{
    internal class Shader : DynamicAsset
    {
        private static readonly List<Shader> CompiledShaders = [];


        public Shader(string path) : base(path)
        {
            OnLoad();
        }

        public int Handle { get; private set; }
        public sealed override void OnLoad()
        {
            if (CompiledShaders.Any(s => s.AssetPath == AssetPath))
            {
                Handle = CompiledShaders.First(s => s.AssetPath == AssetPath).Handle;
                //do not recompile shader again
                return;
            }

            Handle = GL.CreateShader(Path.GetExtension(AssetPath!).ToLower() switch
            {
                ".vert" => ShaderType.VertexShader,
                ".frag" => ShaderType.FragmentShader,
                ".geom" => ShaderType.GeometryShader,
                _ => throw new NotSupportedException("Unknown shader type!")
            });
            var content = File.ReadAllText(AssetPath!);
            GL.ShaderSource(Handle, content);
            GL.CompileShader(Handle);

            if (GL.GetError() != ErrorCode.NoError)
            {
                Log.Error(new Exception(GL.GetShaderInfoLog(Handle)), "Cant compile shader id:{Handle} src:{AssetPath}", Handle, AssetPath);
            }
            else
            {
                Log.Debug("Compiled shader id:{Handle} src:{AssetPath}", Handle, AssetPath);
                CompiledShaders.Add(this);
            }
        }

        public override void OnUnload()
        {
            base.OnUnload();
            if (Handle != 0)
            {
                GL.DeleteShader(Handle);
                Handle = 0;
            }
        }

        public static implicit operator int(Shader s) => s.Handle;
    }
}
