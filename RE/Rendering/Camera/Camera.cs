using ImGuiNET;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using RE.Core;

namespace RE.Rendering.Camera
{
    public class Camera
    {
        public static Camera Instance { get; private set; }

        private Vector2 _lastMousePos;
        private bool _firstMove = true;

        public Vector3 Position;
        public Vector3 Front = -Vector3.UnitZ;
        public Vector3 Up;
        public float Yaw = -90f;
        public float Pitch = 0f;
        public float AspectRatio;

        private const float MouseSensitivity = 0.2f;

        private Camera() { }
        private Camera(Vector3 position, Vector3 up, float aspectRatio)
        {
            Position = position;
            Up = up;
            AspectRatio = aspectRatio;
        }

        public static void Init()
        {
            Instance = new Camera(Vector3.Zero, Vector3.UnitY, ((float)Game.Instance.ClientSize.X / (float)Game.Instance.ClientSize.Y));
            Game.Instance.CursorState = CursorState.Grabbed;
            Game.Instance.MouseMove += (s) => Instance.HandleMouseMove(s.X, s.Y);
            Game.Instance.UpdateFrame += (_) => Instance.HandleInput(Game.Instance.KeyboardState);
            Game.Instance.MouseDown += (_) =>
            {
                if (!ImGui.GetIO().WantCaptureMouse)
                    Game.Instance.CursorState = CursorState.Grabbed;
            };
        }
        public void HandleMouseMove(float mouseX, float mouseY)
        {
            if (Game.Instance.CursorState != CursorState.Grabbed || ImGui.GetIO().WantCaptureMouse) return;
            if (_firstMove)
            {
                _lastMousePos = new Vector2(mouseX, mouseY);
                _firstMove = false;
                return;
            }

            var deltaX = mouseX - _lastMousePos.X;
            var deltaY = _lastMousePos.Y - mouseY; // инверсия Y

            _lastMousePos = new Vector2(mouseX, mouseY);

            Yaw += deltaX * MouseSensitivity;
            Pitch += deltaY * MouseSensitivity;

            Pitch = MathHelper.Clamp(Pitch, -89f, 89f);

            Vector3 front;
            front.X = MathF.Cos(MathHelper.DegreesToRadians(Yaw)) * MathF.Cos(MathHelper.DegreesToRadians(Pitch));
            front.Y = MathF.Sin(MathHelper.DegreesToRadians(Pitch));
            front.Z = MathF.Sin(MathHelper.DegreesToRadians(Yaw)) * MathF.Cos(MathHelper.DegreesToRadians(Pitch));
            Front = Vector3.Normalize(front);
        }
        public void HandleInput(KeyboardState state)
        {
            var input = state;
            float speed = 2.5f * Time.DeltaTime;


            if (input.IsKeyDown(Keys.W))
                Position += Front * speed;
            if (input.IsKeyDown(Keys.S))
                Position -= Front * speed;
            if (input.IsKeyDown(Keys.A))
                Position -= Vector3.Normalize(Vector3.Cross(Front, Up)) * speed;
            if (input.IsKeyDown(Keys.D))
                Position += Vector3.Normalize(Vector3.Cross(Front, Up)) * speed;
            if (input.IsKeyDown(Keys.Escape))
                Game.Instance.CursorState = CursorState.Normal;
        }
        public Matrix4 GetViewMatrix()
            => Matrix4.LookAt(Position, Position + Front, Up);

        public Matrix4 GetProjectionMatrix()
            => Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(60f), AspectRatio, 0.1f, 100f);
    }
}
