using ImGuiNET;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using RE.Core;
using RE.Debug;
using RE.Debug.Overlay;
using RE.Rendering.Renderables;
using RE.Utils;
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
                LineManager.Main!.AddLine(Instance.Position, Instance.Position + Instance.Front * 3f, new Vector4(1, 0, 0, 1),
                    new Vector4(0, 0, 0, 1));
            if (args.Button == MouseButton.Button2)
            {
                new ModelRenderer("assets/models/test.fbx", Instance.Position + Instance.Front * 6).Render();
            }
        };
        (fr = new()).Render();

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
            ConsoleWindow.Instance!.IsVisible = !ConsoleWindow.Instance.IsVisible;
        if (input.IsKeyPressed(Keys.F11))
            Game.Instance.ToggleFullscreen();
        if (input.IsKeyPressed(Keys.F1))
        {
            fr.Clear();
            if (input.IsKeyDown(Keys.LeftShift))
            {
                return;
            }

            var corners = GetFrustumCorners(Camera.Instance.GetProjectionMatrix(), Camera.Instance.GetViewMatrix());
            fr.AddLine(corners[0], corners[1], new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), 0);
            fr.AddLine(corners[1], corners[2], new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), 0);
            fr.AddLine(corners[2], corners[3], new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), 0);
            fr.AddLine(corners[3], corners[0], new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), 0);
            fr.AddLine(corners[4], corners[5], new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), 0);
            fr.AddLine(corners[5], corners[6], new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), 0);
            fr.AddLine(corners[6], corners[7], new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), 0);
            fr.AddLine(corners[7], corners[4], new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), 0);

            // линии соединяющие near и far плоскости  
            for (int i = 0; i < 4; i++)
            {
                fr.AddLine(corners[i], corners[i + 4], new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), 0);
            }
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
    Vector3[] GetFrustumCorners(Matrix4 proj, Matrix4 view, float maxDistance = 1000f)
    {
        Matrix4 inv = Matrix4.Invert(view * proj);
        Vector3[] ndcCorners = new Vector3[]
        {
            new Vector3(-1, -1, -1), // near bottom left
            new Vector3(1, -1, -1),  // near bottom right
            new Vector3(1, 1, -1),   // near top right
            new Vector3(-1, 1, -1),  // near top left

            new Vector3(-1, -1, 1),  // far bottom left
            new Vector3(1, -1, 1),   // far bottom right
            new Vector3(1, 1, 1),    // far top right
            new Vector3(-1, 1, 1)    // far top left
        };

        Vector3[] worldCorners = new Vector3[8];

        for (int i = 0; i < 8; i++)
        {
            Vector4 corner = new Vector4(ndcCorners[i], 1.0f);
            Vector4 worldPos = Vector4.TransformRow(corner, inv);
            worldPos /= worldPos.W; // перспективное деление

            Vector3 pos = worldPos.Xyz;

            float length = pos.Length;
            if (length > maxDistance)
            {
                pos = pos.Normalized() * maxDistance;
            }
            worldCorners[i] = pos;
        }
        return worldCorners;
    }

    public static LineManager fr = new LineManager();

    public Matrix4 GetViewMatrix()
    {
        return Matrix4.LookAt(Position, Position + Front, Up);
    }
    public Matrix4 GetProjectionMatrix()
    {
        return Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(60), AspectRatio, 0.1f, 10000f);
    }
    public Matrix4 GetBillboard(Vector3 objectPosition)
    {
        Matrix4 view = GetViewMatrix();

        view.Row3.Xyz = Vector3.Zero;
        return Matrix4.Transpose(view);
    }

}