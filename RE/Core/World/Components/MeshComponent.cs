using System.Text.Json.Nodes;
using OpenTK.Windowing.Common;
using RE.Core.Scripting.Attributes;
using RE.Editor;
using RE.Rendering.Renderables;

namespace RE.Core.World.Components
{
    [ComponentInfo("World", Description = $"MeshComponent renders object's model, specified by the '{nameof(Path)}' property")]
    public class MeshComponent : Component, IEditorUpdate, IEditorRender
    {
        public readonly ModelRenderer ModelRenderer;

        public MeshComponent()
        {
            ModelRenderer = new ModelRenderer();
        }
        public MeshComponent(string modelPath)
        {
            ModelRenderer = new ModelRenderer(modelPath);
        }

        public ModelRenderer GetModelRenderer() => ModelRenderer;

        [EditorProperty("Model Path")]
        public string Path
        {
            get => ModelRenderer.Path;
            set => ModelRenderer.Path = value;
        }

        public override void Start()
        {
            ModelRenderer.AddedToRenderList();
        }

        public override void Render(FrameEventArgs args)
        {
            ModelRenderer.Position = Owner.Transform.Position;
            ModelRenderer.Rotation = Owner.Transform.Rotation;
            ModelRenderer.Scale = Owner.Transform.Scale;
            if (!string.IsNullOrWhiteSpace(Path))
                ModelRenderer.Render(args);
        }

        public override void OnDestroy()
        {
            ModelRenderer.RemovedFromRenderList();
        }

        public override JsonNode GetSaveData()
        {
            JsonObject root = new();
            var args = new JsonArray { Path };
            root.Add("args", args);
            return root;
        }

        /// <inheritdoc />
        public void EditorUpdate(FrameEventArgs args) => Update(args);

        /// <inheritdoc />
        public void EditorRender(FrameEventArgs args) => Render(args);
    }
}
