using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using Serilog;
using System.Runtime.CompilerServices;
using ErrorCode = OpenTK.Graphics.OpenGL4.ErrorCode;

namespace RE.Libs.Grille.ImGuiTK;

public class GLRenderer : IDisposable
{
    public GLRenderer()
    {
        Objects = new GLObjects();
        State = new GLState();
    }

    public GLObjects Objects { get; }
    public GLState State { get; }

    public bool DebugPrintEnabled { get; set; }

    public void Dispose()
    {
        Objects.Dispose();
    }

    public void RenderImDrawData()
    {
        RenderImDrawData(ImGui.GetDrawData());
    }

    public void RenderImDrawData(ImDrawDataPtr draw_data)
    {
        if (draw_data.CmdListsCount == 0) return;

        State.Setup();
        Objects.Bind();
        Objects.GuaranteeBufferSize(draw_data);

        // Setup orthographic projection matrix into our constant buffer
        var io = ImGui.GetIO();
        Objects.UpdateShader(io);
        CheckGLError("Projection");

        draw_data.ScaleClipRects(io.DisplayFramebufferScale);

        // Render command lists
        for (var n = 0; n < draw_data.CmdListsCount; n++)
        {
            var cmd_list = draw_data.CmdLists[n];

            GL.BufferSubData(BufferTarget.ArrayBuffer, nint.Zero, cmd_list.VtxBuffer.Size * Unsafe.SizeOf<ImDrawVert>(),
                cmd_list.VtxBuffer.Data);
            CheckGLError($"Data Vert {n}");

            GL.BufferSubData(BufferTarget.ElementArrayBuffer, nint.Zero, cmd_list.IdxBuffer.Size * sizeof(ushort),
                cmd_list.IdxBuffer.Data);
            CheckGLError($"Data Idx {n}");

            for (var cmd_i = 0; cmd_i < cmd_list.CmdBuffer.Size; cmd_i++)
            {
                var pcmd = cmd_list.CmdBuffer[cmd_i];
                if (pcmd.UserCallback != nint.Zero) throw new NotImplementedException();

                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, (int)pcmd.TextureId);
                CheckGLError("Texture");

                // We do _windowHeight - (int)clip.W instead of (int)clip.Y because gl has flipped Y when it comes to these coordinates
                var clip = pcmd.ClipRect;
                GL.Scissor((int)clip.X, (int)draw_data.DisplaySize.Y - (int)clip.W, (int)(clip.Z - clip.X),
                    (int)(clip.W - clip.Y));
                CheckGLError("Scissor");

                if ((io.BackendFlags & ImGuiBackendFlags.RendererHasVtxOffset) != 0)
                    GL.DrawElementsBaseVertex(PrimitiveType.Triangles, (int)pcmd.ElemCount,
                        DrawElementsType.UnsignedShort, (nint)(pcmd.IdxOffset * sizeof(ushort)),
                        unchecked((int)pcmd.VtxOffset));
                else
                    GL.DrawElements(BeginMode.Triangles, (int)pcmd.ElemCount, DrawElementsType.UnsignedShort,
                        (int)pcmd.IdxOffset * sizeof(ushort));
                CheckGLError("Draw");
            }
        }

        State.Restore();

        CheckGLError("End of frame");
    }

    private void CheckGLError(string title)
    {
        if (!DebugPrintEnabled) return;

        ErrorCode error;
        var i = 1;
        while ((error = GL.GetError()) != ErrorCode.NoError) Log.Error($"{title} ({i++}): {error}");
    }
}