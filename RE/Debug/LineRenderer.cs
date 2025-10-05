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
    //REMOVE AND REMAKE!!!
    public LineRenderer() => Init();

    public static LineRenderer? Main
    {
        get => field ??= new LineRenderer();
        private set;
    } = null!;

    public static void DrawLine(Vector3 start, Vector3 end, Vector4 colorStart, Vector4 colorEnd) => Main?.RenderLine(start, end, colorStart, colorEnd);


    private readonly List<LineEntry> _lines = new();
    private int _nextId;
    private readonly HashSet<int> _toRemove = new();
    private int _vao, _vbo;
    private ShaderProgram _shaderProgram;

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

        if (_shaderProgram != 0)
        {
            GL.DeleteProgram(_shaderProgram);
            _shaderProgram = null!;
        }
    }


    public override RenderLayer RenderLayer => RenderLayer.World;

    public override bool IsVisible { get; set; } = true;

    //FIXME: https://chatgpt.com/c/685c2b7b-58ec-800b-8dfb-a5da27ba6717
    public override void Render(FrameEventArgs args)
    {
        if (_lines.Count == 0)
            return;

        _shaderProgram.Use();
        var view = Camera.Instance.GetViewMatrix();
        var proj = Camera.Instance.GetProjectionMatrix();

        _shaderProgram.SetValue("uView", view);
        _shaderProgram.SetValue("uProjection", proj);

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

    public void RenderLine(Vector3 start, Vector3 end, Vector4 colorStart, Vector4 colorEnd)
    {
        _shaderProgram.Use();
        var view = Camera.Instance.GetViewMatrix();
        var proj = Camera.Instance.GetProjectionMatrix();

        _shaderProgram.SetValue("uView", view);
        _shaderProgram.SetValue("uProjection", proj);

        var vertexData = new Vertex[2];
        vertexData[0] = new Vertex { Position = start, Color = colorStart };
        vertexData[1] = new Vertex { Position = end, Color = colorEnd };


        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertexData.Length * Marshal.SizeOf<Vertex>(), vertexData,
            BufferUsageHint.DynamicDraw);

        GL.DrawArrays(PrimitiveType.Lines, 0, vertexData.Length);
        GL.BindVertexArray(0);
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
        GL.BufferData(BufferTarget.ArrayBuffer, 0, nint.Zero, BufferUsageHint.DynamicDraw);

        var stride = Vector3.SizeInBytes + Vector4.SizeInBytes;

        GL.EnableVertexAttribArray(0); // position
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);

        GL.EnableVertexAttribArray(1); // color
        GL.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, stride, Vector3.SizeInBytes);

        GL.BindVertexArray(0);
    }


    public int AddLine(Vector3 start, Vector3 end, Vector4 colorStart, Vector4 colorEnd, int msRemove = 10000)
    {
        //    if (!_inited)
        //        throw new InvalidOperationException("LineRenderer is not initialized. Call Init() first.");


        var id = _nextId++;
        _lines.Add(new LineEntry
        {
            Start = start,
            End = end,
            ColorStart = colorStart,
            ColorEnd = colorEnd,
            Id = id
        });
        if (msRemove != 0)
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
        if (_toRemove.Count == 0)
            return;
        _lines.RemoveAll(l => _toRemove.Contains(l.Id));
        _lines.TrimExcess();
        _toRemove.Clear();
        if (!_lines.Any())
            _nextId = 0;
    }

    public void RemoveLine(int id)
    {
        _lines.RemoveAll(l => l.Id == id);
    }

    public void Clear()
    {
        _lines.Clear();
    }

    private struct Vertex
    {
        [UsedImplicitly] public Vector3 Position;
        [UsedImplicitly] public Vector4 Color;
    }

    private class LineEntry
    {
        public Vector4 ColorStart, ColorEnd;
        public int Id;
        public Vector3 Start, End;
    }
}