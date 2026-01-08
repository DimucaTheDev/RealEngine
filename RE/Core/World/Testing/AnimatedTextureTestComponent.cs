using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Nodes;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core.World.Components;
using RE.Editor.Notification;
using RE.Rendering.Texturing;
using Serilog;

namespace RE.Core.World.Testing
{
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
            GetComponent<MeshComponent>()!.ModelRenderer.SetTexture(a);
            string content = """
                             ~   - Open Console (type 'help')
                             E   - Interact
                             F2  - Make screenshot
                             F3  - Wireframe
                             F4  - Open Editor
                             F7  - Toast test
                             F11 - Fullscreen
                             """;
            ToastManager.InsertNotification(new Toast(ToastType.Info, content, "Welcome! Welcome to City 17.", 10));
            try
            {
                throw new Exception("Damn Exception Message");
            }
            catch (Exception e)
            {
                ToastManager.InsertNotification(new Toast(ToastType.Error, e.ToString(), "Damn..."));
            }
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

        /// <inheritdoc />
        public override JsonNode GetSaveData()
        {
            return GetDataForProperties();
        }
    }
}
