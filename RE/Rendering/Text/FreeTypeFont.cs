using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Serilog;
using SharpFont;

namespace RE.Rendering.Text;

public class FreeTypeFont
{
    private readonly Dictionary<uint, Character> _characters = new();
    private readonly int _vao;
    private readonly int _vbo;
    private nint hLib;
    public readonly uint PixelHeight;

    public FreeTypeFont(uint pixelheight, string ttfPath)
    {
        PixelHeight = pixelheight;
        var lib = new Library();

        Stream resource_stream = File.OpenRead(ttfPath);
        var ms = new MemoryStream();
        resource_stream.CopyTo(ms);
        var face = new Face(lib, ms.ToArray(), 0);

        face.SetPixelSizes(0, pixelheight);

        // set 1 byte pixel alignment 
        GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1);

        // set texture unit
        GL.ActiveTexture(TextureUnit.Texture0);

        // Load first 128 characters of ASCII set
        for (uint c = 0; c < 128; c++)
            try
            {
                // load glyph
                //face.LoadGlyph(c, LoadFlags.Render, LoadTarget.Normal);
                face.LoadChar(c, LoadFlags.Render, LoadTarget.Normal);
                var glyph = face.Glyph;
                var bitmap = glyph.Bitmap;

                // create glyph texture
                var texObj = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, texObj);
                GL.TexImage2D(TextureTarget.Texture2D, 0,
                    PixelInternalFormat.R8, bitmap.Width, bitmap.Rows, 0,
                    PixelFormat.Red, PixelType.UnsignedByte, bitmap.Buffer);

                // set texture parameters
                GL.TextureParameter(texObj, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                GL.TextureParameter(texObj, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                GL.TextureParameter(texObj, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                GL.TextureParameter(texObj, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

                // add character
                var ch = new Character();
                ch.TextureID = texObj;
                ch.Size = new Vector2(bitmap.Width, bitmap.Rows);
                ch.Bearing = new Vector2(glyph.BitmapLeft, glyph.BitmapTop);
                ch.Advance = glyph.Advance.X.Value;
                _characters.Add(c, ch);
            }
            catch (Exception ex)
            {
                Log.Error("Error Initializing font!", ex);
            }

        // bind default texture
        GL.BindTexture(TextureTarget.Texture2D, 0);

        // set default (4 byte) pixel alignment 
        GL.PixelStore(PixelStoreParameter.UnpackAlignment, 4);

        float[] vquad =
        {
            // x      y      u     v    
            0.0f, -1.0f, 0.0f, 0.0f,
            0.0f, 0.0f, 0.0f, 1.0f,
            1.0f, 0.0f, 1.0f, 1.0f,
            0.0f, -1.0f, 0.0f, 0.0f,
            1.0f, 0.0f, 1.0f, 1.0f,
            1.0f, -1.0f, 1.0f, 0.0f
        };

        // Create [Vertex Buffer Object](https://www.khronos.org/opengl/wiki/Vertex_Specification#Vertex_Buffer_Object)
        _vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, 4 * 6 * 4, vquad, BufferUsageHint.StaticDraw);

        // [Vertex Array Object](https://www.khronos.org/opengl/wiki/Vertex_Specification#Vertex_Array_Object)
        _vao = GL.GenVertexArray();
        GL.BindVertexArray(_vao);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * 4, 0);
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * 4, 2 * 4);

        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        GL.BindVertexArray(0);
    }
    public float GetTextHeight(string text, float scale = 1f)
    {
        int lines = text.Count(c => c == '\n') + 1;
        return PixelHeight * scale * lines;
    }

    public float GetTextWidth(string text, float scale = 1f)
    {
        float width = 0f;

        foreach (char c in text.Split('\n')[0])
        {
            if (_characters.TryGetValue(c, out var ch))
            {
                width += (ch.Advance >> 6) * scale; // Advance в 1/64 пикселя, поэтому >> 6
            }
        }

        return width;
    }


    public void RenderText(string text, float x, float y, float scale, Vector2 dir)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var line = 0;
        if (text.IndexOf('\n') > -1)
        {
            foreach (var row in text.Split('\n'))
                RenderText(row, x, y + line++ * scale * PixelHeight, scale, dir);
            return;
        }


        y += scale;
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindVertexArray(_vao);

        var angle_rad = (float)Math.Atan2(dir.Y, dir.X);
        var rotateM = Matrix4.CreateRotationZ(angle_rad);
        var transOriginM = Matrix4.CreateTranslation(new Vector3(x, y, 0f));

        // Iterate through all characters
        var char_x = 0.0f;
        foreach (var c in text)
        {
            if (_characters.ContainsKey(c) == false)
                continue;
            var ch = _characters[c];

            var w = ch.Size.X * scale;
            var h = ch.Size.Y * scale;
            var xrel = char_x + ch.Bearing.X * scale;
            var yrel = (ch.Size.Y - ch.Bearing.Y) * scale;

            // Now advance cursors for next glyph (note that advance is number of 1/64 pixels)
            char_x += (ch.Advance >> 6) *
                      scale; // Bitshift by 6 to get value in pixels (2^6 = 64 (divide amount of 1/64th pixels by 64 to get amount of pixels))

            var scaleM = Matrix4.CreateScale(new Vector3(w, h, 1.0f));
            var transRelM = Matrix4.CreateTranslation(new Vector3(xrel, yrel, 0.0f));

            var modelM = scaleM * transRelM * rotateM * transOriginM; // OpenTK `*`-operator is reversed
            GL.UniformMatrix4(0, false, ref modelM);

            // Render glyph texture over quad
            GL.BindTexture(TextureTarget.Texture2D, ch.TextureID);

            // Render quad
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
        }

        GL.BindVertexArray(0);
        GL.BindTexture(TextureTarget.Texture2D, 0);
    }
}