using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using OpenTK.Mathematics;
using RE.Core.Scripting;
using RE.Rendering.Texturing;
using RE.Utils;
using SceneEditor = RE.Editor.SceneEditor;

namespace RE.Rendering;

public class Camera(Vector3 position, Vector3 up, int width, int height)
{
    public static Camera Main { get; set; }
    public static Camera ViewportCamera { get; set; }

    internal static Camera _activeCamera;

    public float Pitch
    {
        get;
        set
        {
            field = value;
            UpdateVectors();
        }
    }

    public float Yaw
    {
        get;
        set
        {
            field = value;
            UpdateVectors();
        }
    }

    public float Roll
    {
        get;
        set
        {
            field = value;
            UpdateVectors();
        }
    }

    public float Fov
    {
        get;
        set
        {
            field = value;
            UpdateVectors();
        }
    } = 80;
    
    
    public StaticTexture? RenderTexture { get; set; }

    public readonly List<PostProcessLayer> PostProcessLayers =
    [
        new("Assets/Shaders/Pass/Postprocess/ca.frag")
    ];
    
    public required string Name { get; set; }

    public Vector3 Position = position;

    public Vector3 Up = up;
    public Vector3 Front = Vector3.UnitX;
    public Vector3 Right => Vector3.Normalize(Vector3.Cross(Front, Up));

    public int RenderWidth = width;
    public int RenderHeight = height;
    public float AspectRatio => (float)RenderWidth / RenderHeight;

    static Camera()
    {
        _activeCamera = Main = new Camera(Vector3.Zero, Vector3.UnitY, Game.Instance.ClientSize.X,
            Game.Instance.ClientSize.Y)
        {
            Name = "Main Camera"
        };
        ViewportCamera = new Camera(new(10, 10, 10), Vector3.UnitY, Game.Instance.ClientSize.X, Game.Instance.ClientSize.Y)
        {
            Name = "Editor Camera"
        };
        ViewportCamera.LookAt((0, 0, 0));
        Variables.VariableChanged += (s, e) =>
        {
            if (s == "fov")
            {
                Main.Fov = (float)e!;
            }
        };
    }

    public Camera() : this(Vector3.Zero)
    {
    }

    public Camera(Vector3 position) : this(position, Vector3.UnitY, Game.Instance.ClientSize.X,
        Game.Instance.ClientSize.Y)
    {
    }

    public static Camera GetActiveCamera() => _activeCamera;

    public void LookAt(Vector3 target)
    {
        Vector3 dir = Vector3.Normalize(target - Position);

        Pitch = MathHelper.RadiansToDegrees(MathF.Asin(dir.Y));
        Yaw = MathHelper.RadiansToDegrees(MathF.Atan2(dir.Z, dir.X));
    }

    public Matrix4 GetViewMatrix() => Matrix4.LookAt(Position, Position + Front, Up);
    public Matrix4 GetProjectionMatrix() => GetProjectionMatrix(Fov);

    public Matrix4 GetProjectionMatrix(float fov) =>
        Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(fov), AspectRatio, 0.1f, 10000f);

    public Matrix4 GetBillboard(bool lockX = false, bool lockY = false)
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
        {
        }

        billboard.Column0 = new Vector4(right, billboard.Column0.W);
        billboard.Column1 = new Vector4(up, billboard.Column1.W);
        billboard.Column2 = new Vector4(forward, billboard.Column2.W);

        return billboard;
    }

    private void UpdateVectors()
    {
        float pitchRad = MathHelper.DegreesToRadians(Pitch);
        float yawRad = MathHelper.DegreesToRadians(Yaw);
        float rollRad = MathHelper.DegreesToRadians(Roll);

        Vector3 front;
        front.X = MathF.Cos(yawRad) * MathF.Cos(pitchRad);
        front.Y = MathF.Sin(pitchRad);
        front.Z = MathF.Sin(yawRad) * MathF.Cos(pitchRad);
        Front = Vector3.Normalize(front);

        Vector3 worldUp = Vector3.UnitY;

        if (MathF.Abs(Front.Y) > 0.999f)
        {
            worldUp = Front.Y > 0 ? -Vector3.UnitZ : Vector3.UnitZ;
        }

        Vector3 baseRight = Vector3.Normalize(Vector3.Cross(Front, worldUp));
        Vector3 baseUp = Vector3.Normalize(Vector3.Cross(baseRight, Front));

        float cosRoll = MathF.Cos(rollRad);
        float sinRoll = MathF.Sin(rollRad);

        Up = Vector3.Normalize(baseUp * cosRoll - baseRight * sinRoll);
    }
}