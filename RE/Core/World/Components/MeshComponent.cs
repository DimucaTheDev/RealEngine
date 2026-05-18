using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using OpenTK.Windowing.Common;
using RE.Core.Assets;
using RE.Core.Scripting.Attributes;
using RE.Editor;
using RE.Rendering.Model;
using RE.Rendering.Renderables;
using RE.Utils;

namespace RE.Core.World.Components
{
    [ComponentInfo("World",
        Description = $"MeshComponent renders object's model, specified by the '{nameof(Path)}' property")]
    public class MeshComponent : Component, IEditorUpdate, IEditorRender
    {
        public readonly ModelRenderer ModelRenderer;

        public MeshComponent()
        {
            ModelRenderer = ModelRenderer.Create("");
        }

        public MeshComponent(string modelPath)
        {
            ModelRenderer = ModelRenderer.Create(modelPath);
        }

        public ModelRenderer GetModelRenderer() => ModelRenderer;

        [EditorProperty("Model Path")]
        public string? Path
        {
            get => ModelRenderer.Model.AssetPath;
            set => ModelRenderer.Model =
                AssetCache.Get(value ?? throw new ArgumentNullException(nameof(value)),
                    ModelLoader.DefaultCacheFactory);
        }

        public override void Render(FrameEventArgs args)
        {
            ModelRenderer.Position = Owner.Transform.Position;
            ModelRenderer.Rotation = Owner.Transform.Rotation;
            ModelRenderer.Scale = Owner.Transform.Scale;

            ModelRenderer.Render(args);
        } 
         
        public void EditorUpdate(FrameEventArgs args) => Update(args);
         
        public void EditorRender(FrameEventArgs args) => Render(args);
    }
}