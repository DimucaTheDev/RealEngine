using System.Text.Json.Nodes;
using OpenCvSharp;
using OpenTK.Windowing.Common;
using RE.Core.World.Components;
using RE.Rendering.Texturing;

namespace RE.Core.World.Testing
{
#pragma warning disable
    internal class Video : Component
    {
        List<StaticTexture> textures = [];
        private float fps;
        public Video()
        {
            using var v = VideoCapture.FromFile("Assets/testing/test.mp4");
            fps = (float)v.Fps;
            frameDuration = 1 / fps;
            using Mat m = new();
            while (v.Read(m))
            {
                throw new NotImplementedException("legacy");
                //byte[] managedArray = new byte[m.];
                //Marshal.Copy(m.Data, managedArray, 0, length);
                //var t = new StaticTexture(managedArray, m.Width, m.Height);
                //textures.Add(t);
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
                GetComponent<SpriteComponent>().Sprite.SetTexture(textures[frame]);
            }

            GetComponent<BillboardTextComponent>().Text = $"frames: {frame}/{textures.Count} {frameDuration * frame:0.0}/{frameDuration * textures.Count:0.0} s";
        }

        public override void OnDestroy()
        {
            foreach (var texture in textures)
            {
                texture.Delete();
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
