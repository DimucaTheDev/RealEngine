using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using OpenTK.Windowing.Common;
using RE.Core.Assets;
using RE.Core.Scripting.Attributes;
using RE.Editor;
using RE.Rendering.Model;
using RE.Rendering.Renderables;
using RE.Utils;
using Serilog;

namespace RE.Core.World.Components
{
    [ComponentInfo("World",
        Description = $"MeshComponent renders object's model, specified by the '{nameof(Path)}' property")]
    public class MeshComponent : Component, IEditorUpdate, IEditorRender
    {
        public readonly ModelRenderer ModelRenderer;

        public MeshComponent()
        {
            ModelRenderer = ModelRenderer.Create();
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
            set
            {
                if (value.IsNullOrEmpty())
                {
                    Log.Error("The {Name} property must be specified.", nameof(Path));
                    return;
                }

                ModelRenderer.Model = AssetCache.Get(value, ModelLoader.DefaultModelCacheFactory);
            }
        }

        public override void Render(double args)
        {
            ModelRenderer.Position = Owner.Transform.Position;
            ModelRenderer.Rotation = Owner.Transform.Rotation;
            ModelRenderer.Scale = Owner.Transform.Scale;

            ModelRenderer.Render(args);
        }

        public void EditorUpdate(double delta) => Update(delta);

        public void EditorRender(double delta) => Render(delta);
    }
}