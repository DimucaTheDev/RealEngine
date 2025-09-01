using OpenTK.Mathematics;
using GL = OpenTK.Graphics.OpenGL4.GL;
using Log = Serilog.Log;

namespace RE.Core.Assets
{
    internal class ShaderProgram : DynamicAsset
    {
        public int Handle { get; private set; } = GL.CreateProgram();

        private bool _linked;
        private readonly List<int> _linkedShaders = [];

        public void AttachShader(string path) => AttachShader(new Shader(path));
        public void AttachShader(Shader shader)
        {
            GL.AttachShader(this, shader);
            _linkedShaders.Add(shader);
        }

        public void Use()
        {
            if (!_linked)
            {
                GL.LinkProgram(this);

                _linkedShaders.ForEach(GL.DeleteShader);
                _linked = true;
            }
            GL.UseProgram(this);
        }

        public void SetValue<T>(string name, T value) where T : notnull
        {
            int location = GL.GetUniformLocation(this, name);
            if (location == -1)
            {
                Log.Error(new InvalidOperationException(), "Unknown uniform location: {Name}", name);
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
                    throw new NotSupportedException($"Uniform doesnt support {typeof(T)}");
            }
        }

        public override void OnUnload()
        {
            base.OnUnload();

            if (Handle != 0)
            {
                GL.DeleteProgram(Handle);
                Handle = 0;
            }
            _linkedShaders.Clear();
        }


        public static implicit operator int(ShaderProgram s) => s.Handle;
    }
}
