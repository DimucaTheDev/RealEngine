using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Backends.Vulkan;
using Hexa.NET.ImGuizmo;
using OpenTK.Graphics.ES11;
using OpenTK.Mathematics;
using RE.Core;
using RE.Core.World;
using RE.Rendering;
using RE.Utils;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;
using TkVector3 = OpenTK.Mathematics.Vector3;
using static Hexa.NET.ImGui.ImGui;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace RE.Debug.Overlay.Editor.Panels
{
    internal class ViewportPanel
    {
        public static Vector2 ViewportSize { get; set; } = new(1280, 720);
        public static bool MouseDown;
        public static Vector2 Position;
        public static Vector2 Size;


        public ImGuizmoOperation Operation = ImGuizmoOperation.Translate;
        public ImGuizmoMode Mode = ImGuizmoMode.World;

        private static readonly Vector3 WorldUp = new Vector3(0, 1, 0);
        private readonly int _translateIcon;
        private readonly int _rotateIcon;
        private readonly int _scaleIcon;
        private readonly int _lightOffIcon;
        private readonly int _lightOnIcon;
        private readonly int _skyboxOffIcon;
        private readonly int _skyboxOnIcon;
        private readonly int _axisOffIcon;
        private readonly int _axisOnIcon;
        private readonly int _gridOffIcon;
        private readonly int _gridOnIcon;

        private readonly string[] _options = ["World", "Local"];
        private int _selectedIndex;

        public ViewportPanel()
        {
            _translateIcon = LoadTexture("assets/sprites/editor/translate.png");
            _rotateIcon = LoadTexture("assets/sprites/editor/rotate.png");
            _scaleIcon = LoadTexture("assets/sprites/editor/scale.png");
            _lightOffIcon = LoadTexture("assets/sprites/editor/lightOff.png");
            _lightOnIcon = LoadTexture("assets/sprites/editor/lightOn.png");
            _skyboxOffIcon = LoadTexture("assets/sprites/editor/skyboxOff.png");
            _skyboxOnIcon = LoadTexture("assets/sprites/editor/skyboxOn.png");
            _axisOffIcon = LoadTexture("assets/sprites/editor/axisOff.png");
            _axisOnIcon = LoadTexture("assets/sprites/editor/axisOn.png");
            _gridOffIcon = LoadTexture("assets/sprites/editor/gridOff.png");
            _gridOnIcon = LoadTexture("assets/sprites/editor/gridOn.png");
        }

        public void Draw()
        {
            if (Begin("Viewport", ImGuiWindowFlags.NoScrollbar))
            {
                DrawToolbar();
                Separator();

                if (IsWindowHovered() && !GetIO().WantCaptureKeyboard && !IsAnyMouseDown())
                    SetWindowFocus();

                if (IsWindowHovered() && IsMouseDown(ImGuiMouseButton.Right))
                {
                    SetWindowFocus();
                }

                MouseDown = (IsWindowFocused() && IsWindowHovered());
                if (IsWindowFocused())
                    MoveCamera();

                PanoramicCameraMove();


                Vector2 contentSize = GetContentRegionAvail();

                if (contentSize is { X: > 0, Y: > 0 } && (ViewportSize.X != contentSize.X || ViewportSize.Y != contentSize.Y))
                {
                    ViewportSize = contentSize;
                    Game.Instance.SetupSceneFbo((int)ViewportSize.X, (int)ViewportSize.Y);
                    Camera.Instance.RenderWidth = (int)ViewportSize.X;
                    Camera.Instance.RenderHeight = (int)ViewportSize.Y;
                }

                Image(new ImTextureRef() { TexID = Game.Instance.SceneTextureId }, ViewportSize,
                    new Vector2(0, 1),
                    new Vector2(1, 0));

                DrawDebugText();
                DrawGrid();

            }
            End();
        }

        private void PanoramicCameraMove()
        {
            bool isMiddleMouseDown = IsMouseDown(ImGuiMouseButton.Middle);
            bool shouldPan = isMiddleMouseDown &&
                             IsWindowFocused();
            if (!shouldPan)
            {
                return;
            }
            Vector2 mouseDelta = GetIO().MouseDelta;

            if (mouseDelta.LengthSquared() == 0)
                return;

            Vector3 up = GetUpVector();
            Vector3 right = GetRightVector();
            float panSpeed = 0.01f;
            Vector3 offset = Vector3.Zero;

            offset -= -right * mouseDelta.X * panSpeed;

            offset += -up * -mouseDelta.Y * panSpeed;

            Camera.Instance.Position += offset;
        }

        private void DrawGrid()
        {
            //var color = new OpenTK.Mathematics.Vector4(0.25f, 0.25f, 0.25f, 0.1f);
            var color = new OpenTK.Mathematics.Vector4(0.75f, 0.75f, 0.75f, 0.3f);
            int size = 80;
            var camPos = (Vector3i)Camera.Instance.Position;

            if (SceneEditor.ShowGrid)
            {
                for (int i = -size; i < size; i++)
                {
                    LineRenderer.DrawLine(new Vector3(camPos.X + i, 0, camPos.Z + -size),
                        new Vector3(camPos.X + i, 0, camPos.Z + size), color, color);
                }

                for (int i = -size; i < size; i++)
                {
                    LineRenderer.DrawLine(new Vector3(camPos.X + -size, 0, camPos.Z + i),
                        new Vector3(camPos.X + size, 0, camPos.Z + i), color, color);
                }
            }

            if (SceneEditor.ShowAxis)
            {
                if (Math.Abs(camPos.Z) < size)
                    LineRenderer.DrawLine(new(camPos.X + -size, 0, 0), new(camPos.X + size, 0, 0), new(1, 0, 0, 1),
                        new(1, 0, 0, 1));
                if (Math.Abs(camPos.X) < size)
                    LineRenderer.DrawLine(new(0, 0, camPos.Z + -size), new(0, 0, camPos.Z + size), new(0, 0, 1, 1),
                        new(0, 0, 1, 1));
                if (Math.Abs(camPos.X) < size && Math.Abs(camPos.Z) < size)
                    LineRenderer.DrawLine(new(0, -size, 0), new(0, size, 0), new(0, 1, 0, 1), new(0, 1, 0, 1));
            }
        }

        private void DrawDebugText()
        {
            string[] data = [
                $"Viewport size: {ViewportSize.X}x{ViewportSize.Y}",
                $"Fov: {Camera.Instance.Fov}°",
                $"Scene: {SceneManager.CurrentScene.Name}",
                $"FPS: {1/Time.DeltaTime:F0}",
                $"Camera pos: {Camera.Instance.Position.X:F2} | {Camera.Instance.Position.Y:F2} | {Camera.Instance.Position.Z:F2}"
            ];
            foreach (var line in data.Index())
            {
                SetCursorPos(new Vector2(15, 70 + CalcTextSize(line.Item).Y * line.Index));
                Text(line.Item);
            }
        }

        private void DrawToolbar()
        {
            var imTextureId1 = new ImTextureID((ulong)_translateIcon);
            var imTextureId2 = new ImTextureID((ulong)_rotateIcon);
            var imTextureId3 = new ImTextureID((ulong)_scaleIcon);
            var imTextureId4 = new ImTextureID((ulong)_lightOffIcon);
            var imTextureId5 = new ImTextureID((ulong)_lightOnIcon);
            var imTextureId6 = new ImTextureID((ulong)_skyboxOffIcon);
            var imTextureId7 = new ImTextureID((ulong)_skyboxOnIcon);
            var imTextureId8 = new ImTextureID((ulong)_axisOffIcon);
            var imTextureId9 = new ImTextureID((ulong)_axisOnIcon);
            var imTextureId10 = new ImTextureID((ulong)_gridOffIcon);
            var imTextureId11 = new ImTextureID((ulong)_gridOnIcon);

            const int size = 20;

            PushStyleColor(ImGuiCol.WindowBg, new Vector4(1, 1, 1, 1)); // ни работойет >_<

            BeginDisabled(Operation == ImGuizmoOperation.Translate);
            if (ImageButton("##translate", new ImTextureRef() { TexID = imTextureId1 }, new Vector2(size, size)))
                Operation = ImGuizmoOperation.Translate;
            EndDisabled();

            BeginDisabled(Operation == ImGuizmoOperation.Rotate);
            SameLine();
            if (ImageButton("##rotate", new ImTextureRef() { TexID = imTextureId2 }, new Vector2(size, size)))
                Operation = ImGuizmoOperation.Rotate;
            EndDisabled();

            BeginDisabled(Operation == ImGuizmoOperation.Scale);
            SameLine();
            if (ImageButton("##scale", new ImTextureRef() { TexID = imTextureId3 }, new Vector2(size, size)))
                Operation = ImGuizmoOperation.Scale;
            EndDisabled();

            SameLine(150);
            AlignTextToFramePadding();
            Text("Object Space: ");

            SameLine();
            SetNextItemWidth(60);
            if (Combo("##tr_space", ref _selectedIndex, _options, _options.Length))
            {
                string selectedSpace = _options[_selectedIndex];
                Mode = selectedSpace switch
                {
                    "Local" => ImGuizmoMode.Local,
                    "World" => ImGuizmoMode.World,
                    _ => Mode
                };
            }

            SameLine(350);

            if (ImageButton("##light", new ImTextureRef() { TexID = SceneEditor.PreviewLight ? imTextureId5 : imTextureId4 }, new Vector2(size, size)))
                SceneEditor.PreviewLight = !SceneEditor.PreviewLight;
            SameLine();
            if (ImageButton("##skybox", new ImTextureRef() { TexID = SceneEditor.PreviewSkybox ? imTextureId7 : imTextureId6 }, new Vector2(size, size)))
                SceneEditor.PreviewSkybox = !SceneEditor.PreviewSkybox;
            SameLine();
            if (ImageButton("##axis", new ImTextureRef() { TexID = SceneEditor.ShowAxis ? imTextureId9 : imTextureId8 }, new Vector2(size, size)))
                SceneEditor.ShowAxis = !SceneEditor.ShowAxis;
            SameLine();
            if (ImageButton("##grid", new ImTextureRef() { TexID = SceneEditor.ShowGrid ? imTextureId11 : imTextureId10 }, new Vector2(size, size)))
                SceneEditor.ShowGrid = !SceneEditor.ShowGrid;

            PopStyleColor();
        }
        private void MoveCamera()
        {
            var p = Camera.Instance.Position;

            var speed = 7f * Time.DeltaTime;
            var input = Game.Instance.KeyboardState;
            var mInput = Game.Instance.MouseState;
            if (mInput.ScrollDelta.Y < 0 && IsWindowHovered())
                p += -(Camera.Instance.Front).Normalized();
            if (mInput.ScrollDelta.Y > 0 && IsWindowHovered())
                p += (Camera.Instance.Front).Normalized();

            if (input.IsKeyDown(Keys.W))
                p += (Camera.Instance.Front with { Y = 0 }).Normalized() * speed;
            if (input.IsKeyDown(Keys.S))
                p -= (Camera.Instance.Front with { Y = 0 }).Normalized() * speed;
            if (input.IsKeyDown(Keys.A))
                p -= TkVector3.Normalize(TkVector3.Cross(Camera.Instance.Front, Camera.Instance.Up)) * speed;
            if (input.IsKeyDown(Keys.D))
                p += TkVector3.Normalize(TkVector3.Cross(Camera.Instance.Front, Camera.Instance.Up)) * speed;
            if (input.IsKeyDown(Keys.Space))
                p += TkVector3.UnitY * speed;
            if (input.IsKeyDown(Keys.LeftShift))
                p -= TkVector3.UnitY * speed;

            if (!GetIO().WantTextInput)
                Camera.Instance.Position = (p);
        }
        private int LoadTexture(string path)
        {
            int t = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, t);

            var pathToFace = path;

            using var image = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(pathToFace);
            var pixels = new byte[4 * image.Width * image.Height];
            image.CopyPixelDataTo(pixels);

            GL.TexImage2D(TextureTarget.Texture2D, 0,
                InternalFormat.Rgba,
                image.Width, image.Height, 0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                pixels);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            return t;
        }
        private Vector3 GetRightVector()
        {
            Vector3 right = Vector3.Cross(WorldUp, Camera.Instance.Front);
            return Vector3.Normalize(right);
        }
        private Vector3 GetUpVector()
        {
            Vector3 right = GetRightVector();
            Vector3 up = Vector3.Cross(Camera.Instance.Front, right);
            return Vector3.Normalize(up);
        }
    }
}