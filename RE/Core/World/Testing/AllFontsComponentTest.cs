using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Nodes;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Rendering.Renderables;
using RE.Rendering.Text;

namespace RE.Core.World.Testing
{
    internal class AllFontsComponentTest : Component
    {
        private List<FloatingText> Texts;

        public AllFontsComponentTest()
        {
            Texts = new List<FloatingText>();
            var freeTypeFont = new FreeTypeFont(32, Fonts.Consolas);

            string input = string.Join(" ",
                Enumerable.Range(0, freeTypeFont.CharacterMap.Count).Select(s => (char)s).ToArray());
            int groupSize = 180;
            StringBuilder sb = new StringBuilder(); 
            for (int i = 0; i < input.Length; i++)
            {
                sb.Append(input[i]);
                if ((i + 1) % groupSize == 0)
                    sb.Append("\n\n");
            }

            string result = sb.ToString();

            Texts.Add(new FloatingText(result, Vector3.Zero, freeTypeFont));
        }
        public override void Render(FrameEventArgs args)
        {
            Texts.ForEach(t =>
            {
                t.Position = Owner.Transform.Position;
                t.Render(args);
            });
        }

        public override JsonNode GetSaveData() => new JsonObject();
    }
}
