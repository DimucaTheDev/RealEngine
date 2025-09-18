using System.Diagnostics.CodeAnalysis;
using OpenTK.Graphics.OpenGL4;
using Log = Serilog.Log;

namespace RE.Core.Assets
{
    /// <summary>
    /// Represents an OpenGL shader asset.
    /// </summary>
    /// <remarks>The Shader class provides functionality for loading and compiling vertex, fragment, and
    /// geometry shaders from file paths. It avoids recompiling shaders that have already been loaded by reusing
    /// existing compiled shader handles.</remarks>
    public class Shader : DynamicAsset
    {
        private static readonly List<Shader> CompiledShaders = [];

        /// <summary>
        /// Initializes a new instance of the Shader class using the specified file path.
        /// </summary>
        /// <remarks>
        /// This constructor loads and compiles the shader from the provided file path.
        /// </remarks>
        /// <param name="path">The file system path to the shader source file. Cannot be null or empty.</param>
        public Shader(string path) : base(path)
        {
            OnLoad();
        }

        /// <summary>
        /// OpenGL handle for the compiled shader.
        /// </summary>
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
            if (!File.Exists(AssetPath))
            {
                Log.Error("Shader {Path} does not exist!", AssetPath);
                GL.DeleteShader(Handle);
                return;
            }
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

        /// <summary>
        /// Returns the OpenGL handle of the shader.
        /// </summary>
        /// <param name="s"></param>
        public static implicit operator int(Shader s) => s.Handle;
    }
}
