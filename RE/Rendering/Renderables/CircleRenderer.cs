using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Utils;

namespace RE.Rendering.Renderables;

public class CircleRenderer : Renderable
{
    private readonly int _vao;
    private readonly int _vbo;
    private readonly int _shaderProgram;
    private Vector3[] _vertices;

    public override RenderLayer RenderLayer => RenderLayer.World;
    public override bool IsVisible { get; set; } = true;
    public Vector3 Center { get; set; }
    public float Radius { get; set; }
    public int Segments { get; set; }
    public Color4 Color { get; set; } = Color4.Red;

    public CircleRenderer(Vector3 pos, float radius = 1, int segments = 64)
    {
        Center = pos;
        Radius = radius;
        Segments = segments;

        _vertices = new Vector3[Segments + 1];
        _vbo = GL.GenBuffer();
        _vao = GL.GenVertexArray();

        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, (Segments + 1) * Vector3.SizeInBytes, nint.Zero, BufferUsageHint.DynamicDraw);

        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, Vector3.SizeInBytes, 0);

        GL.BindVertexArray(0);

        _shaderProgram = CompileShader();
    }

    private int CompileShader()
    {
        var vertexSource = File.ReadAllText("assets/shaders/circle.vert");

        var fragmentSource = File.ReadAllText("assets/shaders/circle.frag");

        int vertexShader = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vertexShader, vertexSource);
        GL.CompileShader(vertexShader);
        CheckShaderCompile(vertexShader);

        int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(fragmentShader, fragmentSource);
        GL.CompileShader(fragmentShader);
        CheckShaderCompile(fragmentShader);

        int program = GL.CreateProgram();
        GL.AttachShader(program, vertexShader);
        GL.AttachShader(program, fragmentShader);
        GL.LinkProgram(program);

        GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int status);
        if (status == 0)
            throw new Exception("Shader linking failed: " + GL.GetProgramInfoLog(program));

        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);

        return program;
    }

    private void CheckShaderCompile(int shader)
    {
        GL.GetShader(shader, ShaderParameter.CompileStatus, out int success);
        if (success == 0)
        {
            var info = GL.GetShaderInfoLog(shader);
            throw new Exception("Shader compile error: " + info);
        }
    }

    public override void Render(FrameEventArgs args)
    {
        UpdateVertices();

        GL.UseProgram(_shaderProgram);
        var viewMatrix = Camera.Instance.GetViewMatrix();
        var projectionMatrix = Camera.Instance.GetProjectionMatrix();
        GL.UniformMatrix4(GL.GetUniformLocation(_shaderProgram, "uView"), false, ref viewMatrix);
        GL.UniformMatrix4(GL.GetUniformLocation(_shaderProgram, "uProj"), false, ref projectionMatrix);
        GL.Uniform4(GL.GetUniformLocation(_shaderProgram, "uColor"), Color);

        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        RenderCircle(viewMatrix, projectionMatrix, Vector3.UnitX, Vector3.UnitY); // XY
        RenderCircle(viewMatrix, projectionMatrix, Vector3.UnitY, Vector3.UnitZ); // YZ
        RenderCircle(viewMatrix, projectionMatrix, Vector3.UnitX, Vector3.UnitZ); // XZ 

        GL.BindVertexArray(0);
    }
    private void RenderCircle(Matrix4 view, Matrix4 proj, Vector3 axis1, Vector3 axis2)
    {
        if (_vertices == null || _vertices.Length != Segments + 1)
            _vertices = new Vector3[Segments + 1];

        float step = 2.0f * MathF.PI / Segments;
        for (int i = 0; i <= Segments; i++)
        {
            float angle = i * step;
            Vector3 point = Center +
                            MathF.Cos(angle) * Radius * axis1 +
                            MathF.Sin(angle) * Radius * axis2;
            _vertices[i] = point;
        }

        GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * Vector3.SizeInBytes, _vertices, BufferUsageHint.DynamicDraw);
        GL.DrawArrays(PrimitiveType.LineLoop, 0, _vertices.Length);
    }

    private void UpdateVertices()
    {
        if (_vertices == null || _vertices.Length != Segments + 1)
            _vertices = new Vector3[Segments + 1];

        float angleStep = 2.0f * MathF.PI / Segments;
        for (int i = 0; i <= Segments; i++)
        {
            float angle = i * angleStep;
            float x = MathF.Cos(angle) * Radius;
            float y = MathF.Sin(angle) * Radius;
            _vertices[i] = Center + new Vector3(x, y, 0);
        }
    }


    public void Dispose()
    {
        this.StopRender();
        GL.DeleteBuffer(_vbo);
        GL.DeleteVertexArray(_vao);
        GL.DeleteProgram(_shaderProgram);
    }
}
