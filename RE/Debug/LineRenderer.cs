using System.Runtime.InteropServices;
using JetBrains.Annotations;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core;
using RE.Core.Assets;
using RE.Rendering;

namespace RE.Debug;

public class LineRenderer : Renderable
{
    private const int MaxVertices = 10000;

    private static LineRenderer? _instance;
    public static LineRenderer Main => _instance ??= new LineRenderer();

    private readonly List<LineEntry> _lines = new(256);
    private readonly HashSet<int> _toRemove = new();
    private int _nextId;

    private int _vao, _vbo;
    private ShaderProgram _shaderProgram;
    private Vertex[] _vertexBatch = new Vertex[MaxVertices];

    public override RenderLayer RenderLayer => RenderLayer.World;
    public override bool IsVisible { get; set; } = true;

    public LineRenderer()
    {
        Init();
        _instance = this;
    }

    public static void DrawLine(Vector3 start, Vector3 end, Vector4 colorStart, Vector4 colorEnd)
        => Main.RenderLine(start, end, colorStart, colorEnd);

    public override void Render(FrameEventArgs args)
    {
        if (_lines.Count == 0 || !IsVisible)
            return;

        RenderInternal();
    }

    public void RenderLine(Vector3 start, Vector3 end, Vector4 colorStart, Vector4 colorEnd)
    {
        AddLine(start, end, colorStart, colorEnd, 0);
    }

    private void RenderInternal()
    {
        _shaderProgram.Use();
        var camera = Camera.GetActiveCamera();
        _shaderProgram.SetValue("uView", camera.GetViewMatrix());
        _shaderProgram.SetValue("uProjection", camera.GetProjectionMatrix());

        int vertexCount = Math.Min(_lines.Count * 2, MaxVertices);

        for (int i = 0; i < _lines.Count && (i * 2 + 1) < MaxVertices; i++)
        {
            ref var line = ref CollectionsMarshal.AsSpan(_lines)[i];
            _vertexBatch[i * 2] = new Vertex { Position = line.Start, Color = line.ColorStart };
            _vertexBatch[i * 2 + 1] = new Vertex { Position = line.End, Color = line.ColorEnd };
        }

        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);

        GL.BufferSubData(BufferTarget.ArrayBuffer, nint.Zero, vertexCount * Marshal.SizeOf<Vertex>(), _vertexBatch);

        GL.DrawArrays(PrimitiveType.Lines, 0, vertexCount);

        _lines.RemoveAll(l => l.DurationMs == 0);
    }

    public void Init()
    {
        _shaderProgram = new ShaderProgram();
        _shaderProgram.AttachShader("Assets/shaders/line.vert");
        _shaderProgram.AttachShader("Assets/shaders/line.frag");

        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();

        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);

        GL.BufferData(BufferTarget.ArrayBuffer, MaxVertices * Marshal.SizeOf<Vertex>(), nint.Zero, BufferUsageHint.DynamicDraw);

        var stride = Marshal.SizeOf<Vertex>();
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, stride, Marshal.OffsetOf<Vertex>(nameof(Vertex.Color)));

        GL.BindVertexArray(0);
    }

    public int AddLine(Vector3 start, Vector3 end, Vector4 colorStart, Vector4 colorEnd, int msRemove = 10000)
    {
        var id = _nextId++;
        _lines.Add(new LineEntry
        {
            Start = start,
            End = end,
            ColorStart = colorStart,
            ColorEnd = colorEnd,
            Id = id,
            DurationMs = msRemove
        });

        if (msRemove > 0)
        {
            Time.Schedule(msRemove, () => ScheduleRemove(id));
        }

        return id;
    }

    public void ScheduleRemove(int id)
    {
        _toRemove.Add(id);
        if (_toRemove.Count == 1)
            Time.Schedule(100, ProcessRemovals);
    }

    public void ProcessRemovals()
    {
        if (_toRemove.Count == 0)
            return;

        _lines.RemoveAll(l => _toRemove.Contains(l.Id));
        _toRemove.Clear();

        if (_lines.Count == 0)
            _nextId = 0;
    }

    public void RemoveLine(int id) => _lines.RemoveAll(l => l.Id == id);

    public void Clear() => _lines.Clear();

    public override void Dispose()
    {
        GL.DeleteVertexArray(_vao);
        GL.DeleteBuffer(_vbo);
        _shaderProgram?.Delete();
        GC.SuppressFinalize(this);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct Vertex
    {
        public Vector3 Position;
        public Vector4 Color;
    }

    private struct LineEntry
    {
        public Vector3 Start;
        public Vector3 End;
        public Vector4 ColorStart;
        public Vector4 ColorEnd;
        public int Id;
        public int DurationMs;
    }
}