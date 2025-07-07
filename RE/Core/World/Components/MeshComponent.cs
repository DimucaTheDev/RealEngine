using OpenTK.Windowing.Common;
using RE.Rendering.Renderables;

namespace RE.Core.World.Components
{
    internal class MeshComponent : Component
    {
        private ModelRenderer _modelRenderer;
        private bool _started;
        public MeshComponent(string modelPath)
        {
            _modelRenderer = new ModelRenderer(modelPath);
        }

        public ModelRenderer GetModelRenderer() => _modelRenderer;

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
    }
}
