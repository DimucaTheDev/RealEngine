using System.Text.Json.Nodes;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core.Scripting.Attributes;
using RE.Core.World.Components;
using RE.Rendering.Texturing;
using RE.Utils;

namespace RE.Core.World.Testing
{
    [RequiresComponent<MeshComponent>]
    internal class AnimatedTextureTestComponent : Component
    {
        public override void Start()
        {
            var a = new AnimatedTexture([
                new("assets/testing/0.png"),
                new("assets/testing/1.png"),
                new("assets/testing/2.png"),
                new("assets/testing/3.png"),
                new("assets/testing/4.png"),
                new("assets/testing/5.png"),
                new("assets/testing/6.png"),
                new("assets/testing/7.png"),
                new("assets/testing/8.png"),
                new("assets/testing/9.png"),
            ], 1);
            //var a = new StaticTexture("assets/testing/1.png");
            GetComponent<MeshComponent>()!.ModelRenderer.Model.SetTexture(a);
        }

        /// <inheritdoc />
        public override void Update(FrameEventArgs args)
        {
            Owner.Transform.RotationXyz +=
                new Vector3(
                    MathF.Cos(Time.ElapsedTime),
                    MathF.Sin(Time.ElapsedTime),
                    MathF.Cos(Time.ElapsedTime))
                * Time.DeltaTime * 75;
        } 
    }
}
