using ImGuiNET;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using RE.Core;
using RE.Debug;
using RE.Debug.Overlay;
using RE.Utils;
using System.Diagnostics;

namespace RE.Rendering;

public class Camera
{
    private const float MouseSensitivity = 0.2f;

    private bool _firstMove = true;

    private Vector2 _lastMousePos;
    public float AspectRatio;
    public Vector3 Front = -Vector3.UnitZ;
    public float Pitch;

    public Vector3 Position;
    public Vector3 Up;
    public float Yaw = -90f;

    private Camera()
    {
    }

    private Camera(Vector3 position, Vector3 up, float aspectRatio)
    {
        Position = position;
        Up = up;
        AspectRatio = aspectRatio;
    }

    public static Camera Instance { get; private set; }

    public static void Init()
    {
        Instance = new Camera(Vector3.Zero, Vector3.UnitY,
            Game.Instance.ClientSize.X / (float)Game.Instance.ClientSize.Y);
        Game.Instance.CursorState = CursorState.Grabbed;
        Game.Instance.MouseMove += s => Instance.HandleMouseMove(s.X, s.Y);
        Game.Instance.UpdateFrame += _ => Instance.HandleInput(Game.Instance.KeyboardState);
        Game.Instance.MouseDown += args =>
        {
            if (ImGui.GetIO().WantCaptureMouse) return;

            Game.Instance.CursorState = CursorState.Grabbed;

            if (args.Button == MouseButton.Button1)
                LineManager.Main.AddLine(Instance.Position, Instance.Position + Instance.Front * 3f, new Vector4(1, 0, 0, 1),
                    new Vector4(0, 0, 0, 1));
            if (args.Button == MouseButton.Button2)
            {
                Console.WriteLine($"ModelRenderer RAM: {Process.GetCurrentProcess().PrivateMemorySize64 / 1024 / 1024} MB");

                new ModelRenderer("Assets/Models/test.fbx", Instance.Position + Instance.Front * 1.2f).Render();
                //SoundManager.Play("npc/headcrab_poison/ph_scream", new SoundPlaybackSettings()
                //{
                //    InWorld = true,
                //    MaxDistance = 3,
                //    ReferenceDistance = .5f,
                //    SourcePosition = Instance.Position + Instance.Front * 1.2f,
                //});
                Console.WriteLine($"ModelRenderer RAM: {Process.GetCurrentProcess().PrivateMemorySize64 / 1024 / 1024} MB");

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
        var speed = 2.5f * Time.DeltaTime;


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
        if (input.IsKeyPressed(Keys.GraveAccent))
            ConsoleWindow.Instance.IsVisible = !ConsoleWindow.Instance.IsVisible;
        if (input.IsKeyPressed(Keys.F11))
            Game.Instance.ToggleFullscreen();


        if (input.IsKeyDown(Keys.Escape))
        {
            Game.Instance.CursorState = CursorState.Normal;
            _firstMove = true;
        }
    }

    public Matrix4 GetViewMatrix()
    {
        return Matrix4.LookAt(Position, Position + Front, Up);
    }

    public Matrix4 GetProjectionMatrix()
    {
        return Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(60f), AspectRatio, 0.1f, 100f);
    }
    public Matrix4 GetBillboard(Vector3 objectPosition)
    {
        Matrix4 view = GetViewMatrix();

        // Инвертируем только поворот (обнуляем позицию)
        view.Row3.Xyz = Vector3.Zero;
        return Matrix4.Transpose(view); // транспонированная матрица без смещения


    }

}