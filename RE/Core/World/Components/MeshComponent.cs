using System.Text.Json.Nodes;
using OpenTK.Windowing.Common;
using RE.Core.Scripting;
using RE.Rendering.Renderables;

namespace RE.Core.World.Components
{
    [ComponentInfo("World", Description = $"MeshComponent renders object's model, specified by the '{nameof(Path)}' property")]
    public class MeshComponent : Component
    {
        public readonly ModelRenderer ModelRenderer;
        private bool _started;

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
            _started = true;
        }

        public override void Update(FrameEventArgs args)
        {
        }

        public override void Render(FrameEventArgs args)
        {
            ModelRenderer.Position = Owner.Transform.Position;
            ModelRenderer.Rotation = Owner.Transform.Rotation;
            ModelRenderer.Scale = Owner.Transform.Scale;
            if (_started && !string.IsNullOrWhiteSpace(Path))
                ModelRenderer.Render(args);
        }

        public override void OnDestroy()
        {
            ModelRenderer.RemovedFromRenderList();
            _started = false;
        }

        public override JsonNode GetSaveData()
        {
            JsonObject root = new();
            var args = new JsonArray { Path };
            root.Add("args", args);
            return root;
        }
    }
}
