using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core.Assets;
using RE.Core.World;
using RE.Editor;
using RE.Rendering.Lighting;
using RE.Rendering.Model;
using RE.Utils;
using Material = RE.Rendering.Lighting.Material;
using PrimitiveType = OpenTK.Graphics.OpenGL4.PrimitiveType;
using Quaternion = OpenTK.Mathematics.Quaternion;

namespace RE.Rendering.Renderables
{
    public class ModelRenderer : Renderable
    {
        public override bool IsVisible { get; set; } = true;

        public required ModelData Model { get; set; }
        public Vector3 Position { get; set; } = Vector3.Zero;
        public Quaternion Rotation { get; set; } = Quaternion.Identity;
        public Vector3 Scale { get; set; } = Vector3.One;
        public bool ConstantSize { get; set; }
        public bool IgnoreLight { get; set; } = false;


        public static ModelRenderer Create() => new()
        {
            Model = new ModelData
            {
                Material = new Material(), Mesh = new()
            }
        };

        public static ModelRenderer Create(string path) =>
            new()
            {
                Model = (ModelData)AssetCache.Get(path, ModelLoader.DefaultModelCacheFactory).Clone()
            };


        public override void Render(FrameEventArgs args)
        {
            var viewPos = Camera.GetActiveCamera().Position;
            float distance = (viewPos - Position).Length;

            const float baseScaleFactor = 0.075f;
            float constantScale = distance * baseScaleFactor;

            Matrix4 model =
                Matrix4.CreateScale(ConstantSize ? new(constantScale) : Scale) *
                Matrix4.CreateFromQuaternion(Rotation) *
                Matrix4.CreateTranslation(Position);
            if (IsVisible)
                Render(model, Camera.GetActiveCamera());
        }

        public void Render(Matrix4 model, Camera camera)
        {
            Matrix4 view = camera.GetViewMatrix();
            Matrix4 proj = camera.GetProjectionMatrix();

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, Model.Material.Texture.AsOpenGl());
            //todo: same for Texture1 and specular map

            var program = Model.Material.ShaderProgram;

            program.Use();
            program.SetValue("model", model);
            program.SetValue("view", view);
            program.SetValue("projection", proj);

            // material.glsl
            program.SetStruct("material", Model.Material.Data);

            var lights = SceneManager.CurrentScene.LightSources;

            //lighting.glsl
            program.SetValue("ignoreLight",
                !SceneEditor.Enabled ? IgnoreLight : (IgnoreLight || !SceneEditor.PreviewLight));
            program.SetValue("hasSpotLight", lights.Any(s => s is SpotLight));
            program.SetValue("hasDirLight", lights.Any(s => s is DirectionalLight));

            foreach (var light in lights)
            {
                light.SetParams(program);
            }
            /*_program.SetStruct("spotLight", new SpotLight()
            {
                Position = Camera.Main.Position,
                Direction = Camera.Main.Front,
                DiffuseColor = Vector3.One,
                SpecularColor = Vector3.One,
                Constant = 1.0f,
                Linear = 0.09f,
                Quadratic = 0.032f,
                CutOff = MathF.Cos(MathHelper.DegreesToRadians(12.5f)),
                OuterCutOff = MathF.Cos(MathHelper.DegreesToRadians(17.5f))
            });*/


            GL.BindVertexArray(Model.Mesh.Vao);

            GL.DrawElements(PrimitiveType.Triangles, Model.Mesh.Indices.Count, DrawElementsType.UnsignedInt, 0);
            GL.BindVertexArray(0);

            GL.UseProgram(ShaderProgram.NoProgram);

            // LineRenderer.DrawLine(Position + (0, 2, 0) + dirLightDirection, Position + (0, 2, 0), new(1, 0, 0, 1), new(0, 0, 1, 1));
        }

        public override void Dispose()
        {
            this.StopRender();
            return;
            //GL.DeleteVertexArray(_vao);
            //GL.DeleteBuffer(_vbo);
            //GL.DeleteBuffer(_ebo);
            //_texture.Delete();
        }
    }
}