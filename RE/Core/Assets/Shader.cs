using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using OpenTK.Graphics.OpenGL4;
using RE.Rendering;
using Log = Serilog.Log;

namespace RE.Core.Assets
{
    /// <summary>
    /// Represents an OpenGL shader asset.
    /// </summary>
    /// <remarks>The Shader class provides functionality for loading and compiling vertex, fragment, and
    /// geometry shaders from file paths. It avoids recompiling shaders that have already been loaded by reusing
    /// existing compiled shader handles.</remarks>
    public partial class Shader : DynamicAsset
    {
        private static readonly List<Shader> CompiledShaders = [];
        private static HashSet<string> _seenDecl = new(); // this hash set stores var's names to prevent variable dupe
         
        /// <summary>
        /// Initializes a new instance of the Shader class using the specified file path.
        /// </summary>
        /// <remarks>
        /// This constructor calls <see cref="OnLoad"/> method, which loads and compiles the shader from the provided file path.
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

        /// <summary>
        /// Gets the type of the shader (Vertex, Fragment, Geometry) based on OpenGL handle.
        /// </summary>
        public ShaderType ShaderType
        {
            get
            {
                GL.GetShader(Handle, ShaderParameter.ShaderType, out var type);
                return (ShaderType)type;
            }
        }

        /// <summary>
        /// Gets the source code of the shader as a string based on OpenGL handle.
        /// </summary>
        public string SourceCode
        {
            get
            {
                GL.GetShader(Handle, ShaderParameter.ShaderSourceLength, out int length);
                GL.GetShaderSource(Handle, length, out _, out string source);
                return source;
            }
        }

        /// <summary>
        /// Loads a shader from given path, preprocesses it, compiles, and sets the OpenGL handle.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If a shader with the same <see cref="AssetPath"/> has already been compiled, this method reuses the existing OpenGL handle.
        /// </para>
        /// <para>
        /// Supported shader file extensions are: <c>.vert</c> for vertex shaders, <c>.frag</c> for fragment shaders,
        /// <c>.geom</c> for geometry shaders, <c>.tesc</c> for tesselation control shader, <c>.tese</c> for tesselation evaluation shader.
        /// </para>
        /// <para>
        /// Before compilation, the shader source is preprocessed to handle custom directives such as <c>#include "file"</c> (see <see cref="PreprocessShader"/>).
        /// </para>
        /// </remarks>
        /// <exception cref="NotSupportedException">Specified shader file extension is not supported or invalid</exception>
        public sealed override void OnLoad()
        {
            if (CompiledShaders.Any(s => s.AssetPath == AssetPath))
            {
                Handle = CompiledShaders.First(s => s.AssetPath == AssetPath).Handle;
                //do not recompile shader again, get cached handle
                return;
            }
            Handle = GL.CreateShader(Path.GetExtension(AssetPath!).ToLower() switch
            {
                ".vert" => ShaderType.VertexShader,
                ".frag" => ShaderType.FragmentShader,
                ".geom" => ShaderType.GeometryShader,
                ".tesc" => ShaderType.TessControlShader,
                ".tese" => ShaderType.TessEvaluationShader,
                _ => throw new NotSupportedException("Unknown shader type!")
            });
            if (!ContentManager.Exists(AssetPath))
            {
                GL.DeleteShader(Handle);
                throw new FileNotFoundException("Shader does not exist!", AssetPath);
            }
            var content = ContentManager.GetString(AssetPath!);

            _seenDecl = new HashSet<string>();
            content = PreprocessShader(content, AssetPath);

            GL.ShaderSource(Handle, content);
            GL.CompileShader(Handle);
            GL.GetShader(Handle, ShaderParameter.CompileStatus, out var status);

            if (status != (int)All.True)
            {
                var shaderInfoLog = GL.GetShaderInfoLog(Handle);
                throw new GlException($"Cant compile shader({Handle}) {AssetPath}. {shaderInfoLog}");
            }

            Log.Debug("Compiled shader({Handle}) {AssetPath}", Handle, AssetPath); 
            CompiledShaders.Add(this);
        }

        /// <summary>
        /// Preprocesses a GLSL shader: handles custom directives (e.g., <c>#include</c>),
        /// removes duplicate <c>#version</c> headers, and generates the final shader source.
        /// </summary>
        /// <param name="content">The GLSL shader source code to preprocess.</param>
        /// <param name="shaderPath">The file path of the shader, used to resolve included files.</param>
        /// <param name="excludeVersionHeader">
        /// If <see langword="true"/>, lines starting with <c>#version</c> are ignored
        /// (required when processing included files).
        /// </param>
        /// <returns>The processed shader source code, ready for compilation.</returns>
        /// <exception cref="FileNotFoundException">
        /// Thrown if a file specified in an <c>#include</c> directive cannot be found.
        /// </exception>
        public string PreprocessShader(string content, string shaderPath, bool excludeVersionHeader = false)
        {
            var regex = DirectiveRegex();
            StringBuilder finalShader = new();
            foreach (var line in content.Split(["\r\n", "\n", "\r"], StringSplitOptions.None)) //todo: check for different line endings
            {
                if (regex.IsMatch(line)) // #directive "value"
                {
                    var match = regex.Match(line);
                    var directive = match.Groups[1].Value;
                    switch (directive)
                    {
                        case "include":
                            var includePath = Path.Combine(Path.GetDirectoryName(shaderPath)!, match.Groups[2].Value);
                            if (!ContentManager.Exists(includePath))
                                throw new FileNotFoundException($"INCLUDE shader not found: '{includePath}'");
                            var src = ContentManager.GetString(includePath);
                            //todo: add check for stackoverflow
                            string includeShader = PreprocessShader(src, includePath, true);
                            finalShader.AppendLine($"\n/* begin include {includePath} */\n{includeShader}\n/* end include {includePath} */\n");
                            break;
                        default:
                            Log.Error("Unknown directive '{Directive}' in shader '{Path}'", directive, shaderPath);
                            break;
                    }
                }
                else
                {
                    if (excludeVersionHeader && line.StartsWith("#version"))
                        continue;
                    if (line.StartsWith("in ") || line.StartsWith("out ") || line.StartsWith("uniform "))
                    {
                        if (!_seenDecl.Add(line.Trim()))
                        {
                            Log.Debug("Duplicate shader declaration skipped: {Line}", line.Trim());
                            continue;
                        }
                    }
                    finalShader.AppendLine(line);
                }
            }

            if (Debugger.IsAttached)
            {
                Directory.CreateDirectory("Debug");
                GL.GetShader(Handle, ShaderParameter.ShaderType, out var type);
                var path = $"Debug/shader_{(ShaderType)type}_{Handle}__{new string(shaderPath.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray())}.txt";
                Log.Verbose("New processed shader: {Path}", path);
                File.WriteAllText(path, finalShader.ToString());
            }
            return finalShader.ToString();
        }

        /// <summary>
        /// Deletes the shader from OpenGL, and sets the <see cref="Handle"/> to 0.
        /// </summary>
        public override void OnUnload()
        {
            base.OnUnload();
            if (Handle != 0)
            {
                GL.DeleteShader(Handle);
                CompiledShaders.Remove(this);
                Handle = 0;
            }
        }

        /// <summary>
        /// Returns the OpenGL handle of the shader.
        /// </summary>
        /// <param name="s"></param>
        public static implicit operator int(Shader s) => s.Handle;

        [GeneratedRegex(@"#([A-Za-z0-9]{2,})(?![~!@#$%^&*()=+_`\-\|\/'\[\]\{\}]|[?.,]*\w)\s+""([^""]+)""", RegexOptions.IgnoreCase)]
        private static partial Regex DirectiveRegex();
    }
}
