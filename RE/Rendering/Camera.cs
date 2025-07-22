using ImGuiNET;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using RE.Core;
using RE.Core.Scripting;
using RE.Core.World;
using RE.Core.World.Components;

namespace RE.Rendering;

public class Camera
{
    private const float MouseSensitivity = 0.2f;
    private Vector2 _lastMousePos;

    public bool FirstMove = true;
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
        Instance = new Camera(new(15, 3, 8), Vector3.UnitY, //todo: refactor
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
        //Game.Instance.UpdateFrame += _ => Instance.HandleInput(Game.Instance.KeyboardState);
        Game.Instance.MouseDown += args =>
        {
            if (ImGui.GetIO().WantCaptureMouse)
                return;

            Game.Instance.CursorState = CursorState.Grabbed;


            if (args.Button == MouseButton.Button2)
            {
                GameObject obj = new GameObject();
                obj.Components.Add(new MeshComponent("assets/models/crate.fbx"));

                Vector3 cameraFrontOpenTK = Instance.Front;

                obj.Components.Add(new BoxColliderComponent());
                var rb = new RigidBodyComponent(20);
                obj.Components.Add(rb);

                SceneManager.CurrentScene.GameObjects.Add(obj);

                BulletSharp.Math.Vector3 cameraFrontBullet = new BulletSharp.Math.Vector3(cameraFrontOpenTK.X, cameraFrontOpenTK.Y, cameraFrontOpenTK.Z);
                rb.RigidBody.Restitution = 0.2f;
                rb.RigidBody.Friction = 1;
                float impulseStrength = 5.0f;
                BulletSharp.Math.Vector3 impulseVector = cameraFrontBullet * impulseStrength;
                rb.RigidBody.ApplyImpulse(impulseVector, BulletSharp.Math.Vector3.Zero);

                obj.SetPosition(2 * cameraFrontOpenTK + Instance.Position);

            }
        };

    }

    public void HandleMouseMove(float mouseX, float mouseY)
    {
        if (Game.Instance.CursorState != CursorState.Grabbed || ImGui.GetIO().WantCaptureMouse)
            return;
        if (FirstMove)
        {
            _lastMousePos = new Vector2(mouseX, mouseY);
            FirstMove = false;
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