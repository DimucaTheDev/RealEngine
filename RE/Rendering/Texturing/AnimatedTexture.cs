using System;
using System.Collections.Generic;
using System.Text;
using Hexa.NET.ImGui;
using RE.Core;

namespace RE.Rendering.Texturing
{
    public class AnimatedTexture(List<StaticTexture> frames, float frameRate) : Texture
    {
        public List<StaticTexture> Frames = frames;
        public float FrameRate { get; set; } = frameRate;

        public StaticTexture? GetFrame(float timeOffset = 0)
        {
            if (Frames.Count == 0)
                return null;

            int frameIndex = (int)((Time.ElapsedTime + timeOffset) * FrameRate) % Frames.Count;
            return Frames[frameIndex];
        }

        public override uint AsOpenGl() => GetFrame()!.AsOpenGl();
        public override ImTextureRef AsImGui() => GetFrame()!.AsImGui();
        public override void Delete() => Frames.ForEach(f => f.Delete());

        public static implicit operator StaticTexture(AnimatedTexture animatedTexture) => animatedTexture.GetFrame()!;
        public static implicit operator ImTextureRef(AnimatedTexture texture) => texture.AsImGui();
    }
}
