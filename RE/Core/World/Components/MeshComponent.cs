using System.Text.Json.Nodes;
using OpenTK.Windowing.Common;
using RE.Core.Scripting;
using RE.Rendering.Renderables;

namespace RE.Core.World.Components
{
    [ComponentInfo("World", Description = $"MeshComponent renders object's model, specified by the '{nameof(Path)}' property")]
    internal class MeshComponent : Component
    {
        protected ModelRenderer _modelRenderer;
        private bool _started;

        public MeshComponent()
        {
            _modelRenderer = new ModelRenderer();
        }
        public MeshComponent(string modelPath)
        {
            _modelRenderer = new ModelRenderer(modelPath);
        }

        public ModelRenderer GetModelRenderer() => _modelRenderer;

        [EditorProperty("Model Path")]
        public string Path
        {
            get => _modelRenderer.Path;
            set => _modelRenderer.Path = value;
        }

        public override void Start()
        {
            _modelRenderer.AddedToRenderList();
            _started = true;
        }

        public override void Update(FrameEventArgs args)
        {
            _modelRenderer.Position = Owner.Transform.Position;
            _modelRenderer.Rotation = Owner.Transform.Rotation;
            _modelRenderer.Scale = Owner.Transform.Scale;
        }

        public override void Render(FrameEventArgs args)
        {
            if (_started)
                _modelRenderer.Render(args);
        }

        public override void OnDestroy()
        {
            _modelRenderer.RemovedFromRenderList();
            _started = false;
        }

        public override JsonNode GetSaveData()
        {
            JsonObject root = new();
            var args = new JsonArray();
            args.Add(Path);
            root.Add("args", args);
            return root;
        }
    }
}
