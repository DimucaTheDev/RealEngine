using System.Runtime.CompilerServices;
using Hexa.NET.ImGui;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Serilog;

namespace RE.External.Grille.ImGuiTK;

internal class GLObjects : IDisposable
{
    public int _fontTexture;
    public int _indexBuffer;
    public int _indexBufferSize;

    public int _shader;
    public int _shaderFontTextureLocation;
    public int _shaderProjectionMatrixLocation;
    public int _vertexArray;
    public int _vertexBuffer;
    public int _vertexBufferSize;

    public void Dispose()
    {
        GL.DeleteVertexArray(_vertexArray);
        GL.DeleteBuffer(_vertexBuffer);
        GL.DeleteBuffer(_indexBuffer);

        GL.DeleteTexture(_fontTexture);
        GL.DeleteProgram(_shader);
    }

    public void Bind()
    {
        // Bind the element buffer (thru the VAO) so that we can resize it.
        GL.BindVertexArray(_vertexArray);
        // Bind the vertex buffer so that we can resize it.
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBuffer);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _indexBuffer);

        GL.UseProgram(_shader);
    }

    public void GuaranteeBufferSize(ImDrawDataPtr draw_data)
    {
        for (var i = 0; i < draw_data.CmdListsCount; i++)
        {
            var cmd_list = draw_data.CmdLists[i];

            var vertexSize = cmd_list.VtxBuffer.Size * Unsafe.SizeOf<ImDrawVert>();
            if (vertexSize > _vertexBufferSize)
            {
                var newSize = (int)Math.Max(_vertexBufferSize * 1.5f, vertexSize);

                GL.BufferData(BufferTarget.ArrayBuffer, newSize, nint.Zero, BufferUsageHint.DynamicDraw);
                _vertexBufferSize = newSize;

                Log.Debug($"Resized dear imgui vertex buffer to new size {_vertexBufferSize}");
            }

            var indexSize = cmd_list.IdxBuffer.Size * sizeof(ushort);
            if (indexSize > _indexBufferSize)
            {
                var newSize = (int)Math.Max(_indexBufferSize * 1.5f, indexSize);
                GL.BufferData(BufferTarget.ElementArrayBuffer, newSize, nint.Zero, BufferUsageHint.DynamicDraw);
                _indexBufferSize = newSize;

                Log.Debug($"Resized dear imgui index buffer to new size {_indexBufferSize}");
            }
        }
    }

    public void UpdateShader(ImGuiIOPtr io)
    {
        var mvp = Matrix4.CreateOrthographicOffCenter(
            0.0f,
            io.DisplaySize.X,
            io.DisplaySize.Y,
            0.0f,
            -1.0f,
            1.0f);

        GL.UniformMatrix4(_shaderProjectionMatrixLocation, false, ref mvp);
        GL.Uniform1(_shaderFontTextureLocation, 0);
    }

    public void CreateDeviceResources()
    {
        _vertexBufferSize = 10000;
        _indexBufferSize = 2000;

        var prevVAO = GL.GetInteger(GetPName.VertexArrayBinding);
        var prevArrayBuffer = GL.GetInteger(GetPName.ArrayBufferBinding);

        _vertexArray = GL.GenVertexArray();
        GL.BindVertexArray(_vertexArray);

        _vertexBuffer = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBuffer);
        GL.BufferData(BufferTarget.ArrayBuffer, _vertexBufferSize, nint.Zero, BufferUsageHint.DynamicDraw);

        _indexBuffer = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _indexBuffer);
        GL.BufferData(BufferTarget.ElementArrayBuffer, _indexBufferSize, nint.Zero, BufferUsageHint.DynamicDraw);

        RecreateFontDeviceTexture();

        var VertexSource = ShaderCode.VertexSource;
        var FragmentSource = ShaderCode.FragmentSource;

        _shader = CreateProgram("ImGui", VertexSource, FragmentSource);
        _shaderProjectionMatrixLocation = GL.GetUniformLocation(_shader, "projection_matrix");
        _shaderFontTextureLocation = GL.GetUniformLocation(_shader, "in_fontTexture");

        var stride = Unsafe.SizeOf<ImDrawVert>();
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 8);
        GL.VertexAttribPointer(2, 4, VertexAttribPointerType.UnsignedByte, true, stride, 16);

        GL.EnableVertexAttribArray(0);
        GL.EnableVertexAttribArray(1);
        GL.EnableVertexAttribArray(2);

        GL.BindVertexArray(prevVAO);
        GL.BindBuffer(BufferTarget.ArrayBuffer, prevArrayBuffer);

        //CheckGLError("End of ImGui setup");
    }

    /// <summary>
    ///     Recreates the device texture used to render text.
    /// </summary>
    public unsafe void RecreateFontDeviceTexture()
    {
        
        var io = ImGui.GetIO();

        // Получаем данные атласа — обязательно вызвать перед использованием TexData
        var width = io.Fonts.TexData.Width;
        var height = io.Fonts.TexData.Height;
        var pixels = io.Fonts.TexData.Pixels;

        // Количество уровней мипмапа (full chain)
        int levels = 1 + (int)Math.Floor(Math.Log(Math.Max(width, height), 2));

        var prevActiveTexture = GL.GetInteger(GetPName.ActiveTexture);
        GL.ActiveTexture(TextureUnit.Texture0);
        var prevTexture2D = GL.GetInteger(GetPName.TextureBinding2D);

        _fontTexture = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, _fontTexture);

        // Выделяем хранилище с нужным числом уровней
        GL.TexStorage2D(TextureTarget2d.Texture2D, levels, SizedInternalFormat.Rgba8, width, height);

        // Формат RGBA, потому что мы вызвали GetTexDataAsRGBA32
        GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, width, height, PixelFormat.Rgba, PixelType.UnsignedByte, (nint)pixels);

        GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

        // Для атласа шрифтов обычно использовать ClampToEdge
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, levels - 1);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);

        // Восстановить state
        GL.BindTexture(TextureTarget.Texture2D, prevTexture2D);
        GL.ActiveTexture((TextureUnit)prevActiveTexture);

        // Установить TexID в ImGui и очистить данные CPU-side
        io.Fonts.TexData.SetTexID((nint)_fontTexture);
        io.Fonts.ClearTexData();
    }


    private static int CreateProgram(string name, string vertexSource, string fragmentSoruce)
    {
        var program = GL.CreateProgram();

        var vertex = CompileShader(name, ShaderType.VertexShader, vertexSource);
        var fragment = CompileShader(name, ShaderType.FragmentShader, fragmentSoruce);

        GL.AttachShader(program, vertex);
        GL.AttachShader(program, fragment);

        GL.LinkProgram(program);

        GL.GetProgram(program, GetProgramParameterName.LinkStatus, out var success);
        if (success == 0)
        {
            var info = GL.GetProgramInfoLog(program);
            Log.Debug("GL.LinkProgram had info log [{Name}]:\n{Info}", name, info);
        }

        GL.DetachShader(program, vertex);
        GL.DetachShader(program, fragment);

        GL.DeleteShader(vertex);
        GL.DeleteShader(fragment);

        return program;
    }

    private static int CompileShader(string name, ShaderType type, string source)
    {
        var shader = GL.CreateShader(type);

        GL.ShaderSource(shader, source);
        GL.CompileShader(shader);

        GL.GetShader(shader, ShaderParameter.CompileStatus, out var success);
        if (success == 0)
        {
            var info = GL.GetShaderInfoLog(shader);
            Log.Debug("GL.CompileShader for shader '{Name}' [{ShaderType}] had info log:\n{Info}", name, type, info);
        }

        return shader;
    }
}