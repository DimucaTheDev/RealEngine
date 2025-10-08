using System.Text.Json.Nodes;
using OpenCvSharp;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using RE.Core.World.Components;

namespace RE.Core.World.Testing
{
#pragma warning disable
    internal class Video : Component
    {
        List<int> textures = [];
        private float fps;
        public Video()
        {
            using var v = VideoCapture.FromFile("Assets/testing/test.mp4");
            fps = (float)v.Fps;
            frameDuration = 1 / fps;
            using Mat m = new();
            while (v.Read(m))
            {
                var t = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, t);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgb,
                    m.Width, m.Height, 0, PixelFormat.Bgr, PixelType.UnsignedByte, m.Data);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                textures.Add(t);
                GL.BindTexture(TextureTarget.Texture2D, 0);
            }
        }

        int frame = 0;
        double frameDuration;
        double timeAccumulator = 0;

        public override void Update(FrameEventArgs args)
        {
            timeAccumulator += args.Time;

            while (timeAccumulator >= frameDuration)
            {
                timeAccumulator -= frameDuration;

                frame = (frame + 1) % textures.Count;
                GetComponent<SpriteComponent>().Sprite.SetTexture((uint)textures[frame]);
            }

            GetComponent<BillboardTextComponent>().Text = $"frames: {frame}/{textures.Count} {frameDuration * frame:0.0}/{frameDuration * textures.Count:0.0} s";
        }

        public override void OnDestroy()
        {
            foreach (var texture in textures)
            {
                GL.DeleteTexture(texture);
            }
            textures.Clear();

            base.OnDestroy();
        }

        public override JsonNode GetSaveData()
        {
            return new JsonObject();
        }
    }
}
