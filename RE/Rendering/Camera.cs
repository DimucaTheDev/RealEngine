using ImGuiNET;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using RE.Core;
using RE.Core.Scripting;
using RE.Core.World;
using RE.Core.World.Components;
using RE.Core.World.Physics;
using RE.Debug.Overlay;
using Serilog;

namespace RE.Rendering;

public class Camera
{
    private const float MouseSensitivity = 0.2f;

    private bool _firstMove = true;

    private Vector2 _lastMousePos;
    public float AspectRatio;
    public Vector3 Front = -Vector3.UnitZ;
    public float Pitch;
    public float Fov = 60;
    public Vector3 Position;
    public Vector3 Up;
    public float Yaw = -90f;

    private Camera() { }

    private Camera(Vector3 position, Vector3 up, float aspectRatio)
    {
        Position = position;
        Up = up;
        AspectRatio = aspectRatio;
    }

    public static Camera Instance { get; private set; }


    public static void Init()
    {
        Instance = new Camera(new(15, 3, 8), Vector3.UnitY,
            Game.Instance.ClientSize.X / (float)Game.Instance.ClientSize.Y);
        Variables.VariableChanged += (s, e) =>
        {
            if (s == "fov")
            {
                Instance.Fov = (float)e!;
            }
        };
        Game.Instance.CursorState = CursorState.Grabbed;
        Game.Instance.MouseMove += s => Instance.HandleMouseMove(s.X, s.Y);
        Game.Instance.UpdateFrame += _ => Instance.HandleInput(Game.Instance.KeyboardState);
        Game.Instance.MouseDown += args =>
        {
            if (ImGui.GetIO().WantCaptureMouse) return;

            Game.Instance.CursorState = CursorState.Grabbed;

            if (args.Button == MouseButton.Button1)
            {
                PhysicsManager.Explode(Vector3.Zero, 10, 10);
            }
            if (args.Button == MouseButton.Button2)
            {
                GameObject obj = new GameObject();
                obj.Components.Add(new MeshComponent("assets/models/crate.fbx"));

                Vector3 cameraFrontOpenTK = Instance.Front;

                obj.Components.Add(new BoxColliderComponent());
                var rb = new RigidBodyComponent();
                obj.Components.Add(rb);

                SceneManager.CurrentScene.GameObjects.Add(obj);

                BulletSharp.Math.Vector3 cameraFrontBullet = new BulletSharp.Math.Vector3(cameraFrontOpenTK.X, cameraFrontOpenTK.Y, cameraFrontOpenTK.Z);
                rb.GetRigidBody().Restitution = 0.2f;
                float impulseStrength = 5.0f;
                BulletSharp.Math.Vector3 impulseVector = cameraFrontBullet * impulseStrength;
                rb.GetRigidBody().ApplyImpulse(impulseVector, BulletSharp.Math.Vector3.Zero);

                obj.SetPosition(2 * cameraFrontOpenTK + Instance.Position);

            }
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
        var deltaY = _lastMousePos.Y - mouseY;

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

    public unsafe void HandleInput(KeyboardState state)
    {
        var input = state;

        if (input.IsKeyPressed(Keys.GraveAccent))
        {
            if (ConsoleWindow.Instance!.IsVisible)
            {
                ConsoleWindow.Instance!.IsVisible = false;
                Game.Instance.CursorState = CursorState.Grabbed;
            }
            else
            {
                ConsoleWindow.Instance!.IsVisible = true;
                Game.Instance.CursorState = CursorState.Normal;
                _firstMove = true;
            }

        }

        if (Game.Instance.CursorState != CursorState.Grabbed || ImGui.GetIO().WantCaptureMouse) return;


        var speed = 7f * Time.DeltaTime;

        if (input.IsKeyDown(Keys.W))
            Position += (Front with { Y = 0 }).Normalized() * speed;
        if (input.IsKeyDown(Keys.S))
            Position -= (Front with { Y = 0 }).Normalized() * speed;
        if (input.IsKeyDown(Keys.A))
            Position -= Vector3.Normalize(Vector3.Cross(Front, Up)) * speed;
        if (input.IsKeyDown(Keys.D))
            Position += Vector3.Normalize(Vector3.Cross(Front, Up)) * speed;
        if (input.IsKeyDown(Keys.Space))
            Position += Vector3.UnitY * speed;
        if (input.IsKeyDown(Keys.LeftShift))
            Position -= Vector3.UnitY * speed;
        if (input.IsKeyPressed(Keys.F11))
            Game.Instance.ToggleFullscreen();
        if (input.IsKeyPressed(Keys.F1))
        {
            RenderManager.RemoveCameraFrustum();
            if (input.IsKeyDown(Keys.LeftShift))
            {
                return;
            }
            RenderManager.CreateCameraFrustum();
        }

        if (input.IsKeyPressed(Keys.F2))
        {
            var p = Game.TakeScreenshot();
            //var text = new ScreenText($"Screenshot saved to {Path.GetFileName(p)}", new Vector2(15, Game.Instance.ClientSize.Y - 20), new FreeTypeFont(32, "assets/fonts/eurostile.otf"), Vector4.One);
            //text.Render();
            //text.Fade();
            Log.Information($"Screenshot saved to {p}");
        }

        if (input.IsKeyDown(Keys.Escape))
        {
            Game.Instance.CursorState = CursorState.Normal;
            _firstMove = true;
        }
    }

    public Matrix4 GetViewMatrix() => Matrix4.LookAt(Position, Position + Front, Up);
    public Matrix4 GetProjectionMatrix() => GetProjectionMatrix(Fov);
    public Matrix4 GetProjectionMatrix(float fov) => Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(fov), AspectRatio, 0.1f, 10000f);
    public Matrix4 GetBillboard(Vector3 objectPosition)
    {
        Matrix4 view = GetViewMatrix();

        view.Row3.Xyz = Vector3.Zero;
        return Matrix4.Transpose(view);
    }

}