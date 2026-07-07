using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using OpenTK.Graphics.OpenGL4;
using RE.Rendering;
using RE.Utils;
using Log = Serilog.Log;

namespace RE.Core.Assets
{
    public partial class Shader : DynamicAsset
    {
        internal readonly List<string> DeclaredUniforms = [];

        private static readonly List<Shader> CompiledShaders = [];
        private const string CommonShaderPath = "Assets/Shaders/Core/Common.glsl";
  
        public int Handle { get; private set; }

        public ShaderType ShaderType
        {
            get
            {
                GL.GetShader(Handle, ShaderParameter.ShaderType, out var type);
                return (ShaderType)type;
            }
        }

        public Shader(string path) : base(path)
        {
            OnLoad();
        }

        public sealed override void OnLoad()
        {
            if (CompiledShaders.Any(s => s.AssetPath == AssetPath))
            {
                Handle = CompiledShaders.First(s => s.AssetPath == AssetPath).Handle;
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

            GL.ObjectLabel(ObjectLabelIdentifier.Shader, Handle, -1, AssetPath);

            if (!ContentManager.Exists(AssetPath))
            {
                GL.DeleteShader(Handle);
                throw new FileNotFoundException("Shader does not exist!", AssetPath);
            }

            var source = ContentManager.GetString(AssetPath!);


            var context = new ShaderCompileContext();
            source = PreprocessShader(source, AssetPath, context);

            DebugDumpIfNeeded(source);

            GL.ShaderSource(Handle, source);
            GL.CompileShader(Handle);

            GL.GetShader(Handle, ShaderParameter.CompileStatus, out var status);


            if (status != (int)All.True)
            {
                var log = GL.GetShaderInfoLog(Handle);
                throw new GlException($"Can't compile shader({Handle}) {AssetPath}. {log}");
            }

            Log.Debug("Compiled shader {Handle},{AssetPath}", Handle, AssetPath);


            CompiledShaders.Add(this);
        }

        private class ShaderCompileContext
        {
            public HashSet<string> IncludeStack = new();
        }

        private string PreprocessShader(string content, string shaderPath, ShaderCompileContext context)
        {
            var dirRegex = DirectiveRegex();
            var sb = new StringBuilder();

            string? versionLine = null;
            bool commonInjected = false;

            foreach (var line in content.Split('\n'))
            {
                var trimmed = line.TrimEnd('\r');

                var directiveMath = dirRegex.Match(trimmed);
                if (directiveMath.Success)
                {
                    var directive = directiveMath.Groups[1].Value;

                    switch (directive)
                    {
                        case "include":
                        {
                            var includeRel = directiveMath.Groups[2].Value;
                            var includePath = includeRel.StartsWith('/')
                                ? includeRel
                                : Path.Combine(Path.GetDirectoryName(shaderPath)!, includeRel);
                            if (includePath.Contains(".."))
                                Log.Warning("Path contains '..', errors may occur in path resolving.");
                            IncludeShader(includePath, context, sb);
                            break;
                        }

                        default:
                            Log.Error("Unknown shader directive '{Directive}' in {Path}", directive, shaderPath);
                            break;
                    }

                    continue;
                }


                var uniformMath = UniformRegex().Match(trimmed);
                if (uniformMath.Success)
                {
                    var name = uniformMath.Groups[1].Value;
                    DeclaredUniforms.Add(name);
                }

                if (trimmed.StartsWith("#version"))
                {
                    versionLine = trimmed;
                    continue;
                }

                if (!commonInjected && !context.IncludeStack.Any())
                {
                    IncludeShader(CommonShaderPath, context, sb);
                    commonInjected = true;
                }

                sb.AppendLine(trimmed);
            }

            if (versionLine != null)
            {
                return versionLine + "\n" + sb;
            }

            return sb.ToString();
        }

        private void IncludeShader(string includePath, ShaderCompileContext context, StringBuilder sb)
        {
            if (!context.IncludeStack.Add(includePath))
                throw new Exception($"Include cycle detected: {includePath}");

            includePath = ContentManager.NormalizePath(includePath);

            if (!ContentManager.Exists(includePath))
                throw new FileNotFoundException($"Include not found: {includePath}");

            var src = ContentManager.GetString(includePath);
            var processed = PreprocessShader(src, includePath, context);

            context.IncludeStack.Remove(includePath);

            sb.AppendLine($"// BEGIN INCLUDE: {includePath}");
            sb.AppendLine(processed);
            sb.AppendLine($"// END INCLUDE: {includePath}");
        }

        private void DebugDumpIfNeeded(string source)
        {
            if (!Debugger.IsAttached && !Game.CommandParseResult.GetValue<bool>("--dump-shaders"))
                return;

            Directory.CreateDirectory("Debug");

            GL.GetShader(Handle, ShaderParameter.ShaderType, out var type);

            var safePath = new string(AssetPath!.ToString()
                .Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)
                .ToArray());

            var path = $"Debug/shader_{(ShaderType)type}_{Handle}__{safePath}.txt";

            File.WriteAllText(path, source);

            Log.Verbose("Shader dump: {Path}", path);
        }

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

        public static implicit operator int(Shader s) => s.Handle;

        [GeneratedRegex(@"#([A-Za-z0-9_]{2,})\s+""([^""]+)""")]
        private static partial Regex DirectiveRegex();

        [GeneratedRegex(@"\buniform\s+\w+\s+(?<name>\w+)\s*;")]
        private static partial Regex UniformRegex();
    }
}