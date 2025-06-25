using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core;
using RE.Rendering;
using RE.Rendering.Camera;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RE.Debug;

public class LineManager : IRenderable, IDisposable
{
    private bool _inited;

    private readonly List<LineEntry> _lines = new();
    private int _nextId;
    private readonly HashSet<int> _toRemove = new();
    private int _vao, _vbo, _shader;
    private readonly List<Vertex> _vertices = new();

    public void Dispose()
    {
        if (_vao != 0)
        {
            GL.DeleteVertexArray(_vao);
            _vao = 0;
        }

        if (_vbo != 0)
        {
            GL.DeleteBuffer(_vbo);
            _vbo = 0;
        }

        if (_shader != 0)
        {
            GL.DeleteProgram(_shader);
            _shader = 0;
        }
    }


    public RenderLayer RenderLayer => RenderLayer.World;

    public bool IsVisible { get; set; } = true;

    //FIXME: https://chatgpt.com/c/685c2b7b-58ec-800b-8dfb-a5da27ba6717
    public void Render(FrameEventArgs args)
    {
        if (_lines.Count == 0)
            return;

        GL.UseProgram(_shader);
        var view = Camera.Instance.GetViewMatrix();
        var proj = Camera.Instance.GetProjectionMatrix();

        GL.UniformMatrix4(GL.GetUniformLocation(_shader, "uView"), false, ref view);
        GL.UniformMatrix4(GL.GetUniformLocation(_shader, "uProjection"), false, ref proj);

        // Собираем массив вершин из _lines
        var vertexData = new Vertex[_lines.Count * 2];
        for (var i = 0; i < _lines.Count; i++)
        {
            vertexData[i * 2 + 0] = new Vertex { Position = _lines[i].Start, Color = _lines[i].ColorStart };
            vertexData[i * 2 + 1] = new Vertex { Position = _lines[i].End, Color = _lines[i].ColorEnd };
        }

        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertexData.Length * Marshal.SizeOf<Vertex>(), vertexData,
            BufferUsageHint.DynamicDraw);

        GL.DrawArrays(PrimitiveType.Lines, 0, vertexData.Length);
        GL.BindVertexArray(0);
    }

    [Conditional("DEBUG")]
    public void Init()
    {
        var vertexShaderSource = File.ReadAllText("Assets/shaders/line.vert");
        var fragmentShaderSource = File.ReadAllText("Assets/shaders/line.frag");

        var vertexShader = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vertexShader, vertexShaderSource);
        GL.CompileShader(vertexShader);

        var fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(fragmentShader, fragmentShaderSource);
        GL.CompileShader(fragmentShader);

        _shader = GL.CreateProgram();
        GL.AttachShader(_shader, vertexShader);
        GL.AttachShader(_shader, fragmentShader);
        GL.LinkProgram(_shader);

        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);

        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();

        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, 0, IntPtr.Zero, BufferUsageHint.DynamicDraw);

        var stride = Vector3.SizeInBytes + Vector4.SizeInBytes;

        GL.EnableVertexAttribArray(0); // position
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);

        GL.EnableVertexAttribArray(1); // color
        GL.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, stride, Vector3.SizeInBytes);

        GL.BindVertexArray(0);

        _inited = true;
    }


    public int AddLine(Vector3 start, Vector3 end, Vector4 colorStart, Vector4 colorEnd, int msRemove = 10000)
    {
        //    if (!_inited)
        //        throw new InvalidOperationException("LineManager is not initialized. Call Init() first.");

#if RELEASE
        return 0;
#endif

        var id = _nextId++;
        _lines.Add(new LineEntry
        {
            Start = start,
            End = end,
            ColorStart = colorStart,
            ColorEnd = colorEnd,
            Id = id
        });
        Time.Schedule(msRemove, () => { ScheduleRemove(id); });
        return id;
    }

    public void ScheduleRemove(int id)
    {
        if (!_toRemove.Any())
            Time.Schedule(500, ProcessRemovals);
        _toRemove.Add(id);
    }

    public void ProcessRemovals()
    {
        if (_toRemove.Count == 0) return;
        _lines.RemoveAll(l => _toRemove.Contains(l.Id));
        _lines.TrimExcess();
        _toRemove.Clear();
        if (!_lines.Any()) _nextId = 0;
    }

    public void RemoveLine(int id)
    {
        _lines.RemoveAll(l => l.Id == id);
    }

    public void Clear()
    {
        _vertices.Clear();
        Dispose();
    }

    private struct Vertex
    {
        public Vector3 Position;
        public Vector4 Color;
    }

    private class LineEntry
    {
        public Vector4 ColorStart, ColorEnd;
        public int Id;
        public Vector3 Start, End;
    }
}