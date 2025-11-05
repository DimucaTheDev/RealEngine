using System.Reflection;
using OpenTK.Mathematics;
using RE.Utils;
using GL = OpenTK.Graphics.OpenGL4.GL;
using Log = Serilog.Log;

namespace RE.Core.Assets
{
    /// <summary>
    /// Represents an OpenGL shader program that manages the attachment, linking, and usage of shader objects for
    /// rendering operations.
    /// </summary>
    /// <remarks>A <see cref="ShaderProgram"/> encapsulates the lifecycle of an OpenGL program object, including attaching
    /// shaders, linking them, and setting uniform values. Use this class to manage GPU shader resources and to set
    /// uniform variables for rendering. This class also provides resource cleanup when unloaded.
    /// Thread safety is not guaranteed; use from the same thread that owns the OpenGL context.</remarks>
    public class ShaderProgram : DynamicAsset
    {
        /// <summary>
        /// OpenGL handle for the shader program.
        /// </summary>
        public int Handle { get; private set; } = GL.CreateProgram();

        private bool _linked;
        private readonly List<Shader> _linkedShaders = [];
        private List<string> _unknownLocations = new();

        /// <inheritdoc cref="GL.AttachShader(int, int)"/>
        public void AttachShader(string path) => AttachShader(new Shader(path));
        /// <inheritdoc cref="GL.AttachShader(int, int)"/>
        public void AttachShader(Shader shader)
        {
            GL.AttachShader(this, shader);
            _linkedShaders.Add(shader);
        }

        /// <inheritdoc cref="GL.UseProgram(int)"/>
        /// <remarks>The program will be linked if not already</remarks>
        public void Use()
        {
            if (!_linked)
            {
                GL.LinkProgram(this);

                _linkedShaders.ForEach(s => GL.DeleteShader(s));
                _linked = true;
            }
            GL.UseProgram(this);
        }

        /// <inheritdoc cref="GL.DeleteProgram(int)"/>>
        public void Delete()
        {
            if (GL.IsProgram(this))
                GL.DeleteProgram(this);
        }

        /// <summary>
        /// Sets the value of a uniform variable in the shader program by name.
        /// </summary>
        /// <remarks>If the specified uniform name does not exist in the shader program, the method logs
        /// an error and does not set any value. Only certain types are supported; attempting to use an unsupported type
        /// will result in an exception.</remarks>
        /// <typeparam name="T">The type of the value to set. Must be a supported uniform type and not null.</typeparam>
        /// <param name="name">The name of the uniform variable to set. Cannot be null.</param>
        /// <param name="value">The value to assign to the uniform variable. Must be of a supported type such as <see langword="int"/>, <see langword="uint"/>, <see langword="float"/>, <see langword="double"/>,
        /// <see langword="bool"/>, <see cref="Vector2"/>, <see cref="Vector3"/>, <see cref="Vector4"/>, <see cref="Color4"/>, <see cref="Matrix2"/>, <see cref="Matrix3"/>, or <see cref="Matrix4"/>.</param>
        /// <exception cref="NotSupportedException">Thrown if the type of <typeparamref name="T"/> is not supported as a uniform variable.</exception>
        public void SetValue<T>(string name, T value) where T : notnull
        {
            if (_unknownLocations.Contains(name))
            {
                // Skip setting value for previously unknown uniform
                //todo: update docs
                return;
            }

            int location = GL.GetUniformLocation(this, name);
            if (location == -1)
            {
                Log.Error("Unknown uniform location: {Name}", name);
                if (!_unknownLocations.Contains(name))
                    _unknownLocations.Add(name);
                return;
            }

            switch (value)
            {
                case int i:
                    GL.Uniform1(location, i);
                    break;
                case uint ui:
                    GL.Uniform1(location, (int)ui);
                    break;
                case float f:
                    GL.Uniform1(location, f);
                    break;
                case double d:
                    GL.Uniform1(location, (float)d);
                    break;
                case bool b:
                    GL.Uniform1(location, b ? 1 : 0);
                    break;

                case Vector2 v2:
                    GL.Uniform2(location, v2);
                    break;
                case Vector3 v3:
                    GL.Uniform3(location, v3);
                    break;
                case Vector4 v4:
                    GL.Uniform4(location, v4);
                    break;
                case Color4 c4:
                    GL.Uniform4(location, c4);
                    break;

                case Matrix2 m2:
                    GL.UniformMatrix2(location, false, ref m2);
                    break;
                case Matrix3 m3:
                    GL.UniformMatrix3(location, false, ref m3);
                    break;
                case Matrix4 m4:
                    GL.UniformMatrix4(location, false, ref m4);
                    break;

                default:
                    throw new NotSupportedException($"Uniform doesn't support {typeof(T)}");
            }
        }

        public void SetStructArray<T>(string varName, IEnumerable<T> values)
        {
            var valArray = values as T[] ?? values.ToArray();

            if (valArray.Length == 0)
                return;

            var methodInfo = GetType().GetMethod(nameof(SetValue));

            foreach (var prop in typeof(T).GetProperties())
            {
                var propNameAttr = prop.GetCustomAttribute<GlPropertyNameAttribute>();
                var propName = propNameAttr?.PropertyName ?? prop.Name;
                var method = methodInfo?.MakeGenericMethod(prop.PropertyType);

                for (int i = 0; i < valArray.Length; i++)
                {
                    var propValue = prop.GetValue(valArray[i]);

                    string uniformName = $"{varName}[{i}].{propName}";

                    if (propValue is null)
                        continue;

                    method?.Invoke(this, [uniformName, propValue]);
                }
            }
        }
        //todo: needs investigation. do we really need nameless vars?
        private void SetStructArray<T>(IEnumerable<T> values)
        {
            var structNameAttr = typeof(T).GetCustomAttribute<GlStructNameAttribute>();
            var structName = structNameAttr?.StructureName ?? typeof(T).Name;
            SetStructArray(structName, values);
        }
        public void SetStruct<T>(string varName, T value) where T : struct
        {
            //var structNameAttr = typeof(T).GetCustomAttribute<GlStructNameAttribute>();
            string structName = varName;//structNameAttr?.StructureName ?? typeof(T).Name;

            foreach (var prop in typeof(T).GetProperties())
            {
                var propNameAttr = prop.GetCustomAttribute<GlPropertyNameAttribute>();
                var propName = propNameAttr?.PropertyName ?? prop.Name;

                string uniformName = $"{structName}.{propName}";
                object? propValue = prop.GetValue(value);

                if (propValue is null)
                    continue;

                var method = GetType()
                    .GetMethod(nameof(SetValue))?
                    .MakeGenericMethod(propValue.GetType());

                method?.Invoke(this, [uniformName, propValue]);
            }
        }

        /// <summary>
        /// Deleted shader program and sets <see cref="Handle"/> to 0.
        /// </summary>
        public override void OnUnload()
        {
            base.OnUnload();

            if (Handle != 0)
            {
                Delete();
                Handle = 0;
            }
            _linkedShaders.Clear();
        }

        public static implicit operator int(ShaderProgram s) => s.Handle;
    }
}
