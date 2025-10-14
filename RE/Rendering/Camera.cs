using Hexa.NET.ImGui;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using RE.Core;
using RE.Core.Scripting;
using RE.Core.World;
using RE.Core.World.Components;
using RE.Core.World.Components.Physics;
using RE.Debug.Overlay;

namespace RE.Rendering;

public class Camera
{
    private const float MouseSensitivity = 0.2f;
    private Vector2 _lastMousePos;
    private Vector2 _lastMouseDownPos;

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
        Game.Instance.MouseUp += args =>
        {
            if (SceneEditor.Enabled && args.Button == MouseButton.Button2)
            {
                if (ImGui.GetIO().WantCaptureMouse || ImGui.GetIO().WantCaptureKeyboard)
                    return;

                Game.Instance.CursorState = CursorState.Normal;
                Game.Instance.MousePosition = Instance._lastMouseDownPos;
            }
        };
        Game.Instance.MouseDown += args =>
        {
            if (ImGui.GetIO().WantCaptureMouse)
                return;

            if (SceneEditor.Enabled)
            {
                ImGui.GetIO().AddMouseButtonEvent(0, true);
                if (args.Button == MouseButton.Button2)
                {
                    Instance._lastMouseDownPos = Game.Instance.MousePosition;
                    Game.Instance.CursorState = CursorState.Grabbed;
                    Instance.FirstMove = true;
                }

                return;
            }

            Game.Instance.CursorState = CursorState.Grabbed;


            if (!SceneEditor.Enabled && args.Button == MouseButton.Button2)
            {
                GameObject obj = new GameObject();
                obj.Components.Add(new MeshComponent("assets/models/crate.fbx"));

                Vector3 front = Instance.Front;

                obj.Components.Add(new BoxColliderComponent());
                var rb = new RigidBodyComponent(20);
                obj.Components.Add(rb);

                SceneManager.CurrentScene.GameObjects.Add(obj);

                BulletSharp.Math.Vector3 cameraFrontBullet = new BulletSharp.Math.Vector3(front.X, front.Y, front.Z);
                rb.RigidBody.Restitution = 0.2f;
                rb.RigidBody.Friction = 1;
                float impulseStrength = 5.0f;
                BulletSharp.Math.Vector3 impulseVector = cameraFrontBullet * impulseStrength;
                rb.RigidBody.ApplyImpulse(impulseVector, BulletSharp.Math.Vector3.Zero);

                obj.SetPosition(2 * front + Instance.Position);
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
    public Matrix4 GetBillboard(Vector3 objectPosition, bool lockX = false, bool lockY = false)
    {
        Matrix4 view = GetViewMatrix();

        view.Row3.Xyz = Vector3.Zero;

        Matrix4 billboard = Matrix4.Transpose(view);

        Vector3 right = billboard.Column0.Xyz;
        Vector3 up = billboard.Column1.Xyz;
        Vector3 forward = billboard.Column2.Xyz;

        if (lockX)
        {
            up = Vector3.UnitY;
            right = Vector3.Normalize(Vector3.Cross(up, forward));
        }

        if (lockY)
        { } // huh

        billboard.Column0 = new Vector4(right, billboard.Column0.W);
        billboard.Column1 = new Vector4(up, billboard.Column1.W);
        billboard.Column2 = new Vector4(forward, billboard.Column2.W);

        return billboard;
    }
}