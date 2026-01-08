using System.Runtime.CompilerServices;
using Hexa.NET.ImGui;
using Hexa.NET.ImGuizmo;
using OpenTK.Audio.OpenAL;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using RE.Core;
using RE.Core.Scripting;
using RE.Core.World;
using RE.Core.World.Components;
using RE.Core.World.Components.Physics;
using RE.Editor.Panels.Viewport;
using SceneEditor = RE.Editor.SceneEditor;

namespace RE.Rendering;

public class Camera(Vector3 position, Vector3 up, int width, int height)
{
    public float AspectRatio => (float)RenderWidth / RenderHeight;
    public Vector3 Front = -Vector3.UnitZ;
    public float Pitch;
    public float Yaw
    {
        get => field;
        set
        {
            field = value;
            UpdateVectors();
        }
    } = -90f;
    public float Fov
    {
        get => field;
        set
        {
            field = value;
            UpdateVectors();
        }
    } = 75;
    public Vector3 Position = position;
    public Vector3 Up = up;
    public int RenderWidth = width, RenderHeight = height;

    public static Camera Main { get; private set; }
    public static Camera Editor { get; private set; }

    public static void Init()
    {
        Main = new Camera(new(15, 3, 8), Vector3.UnitY, Game.Instance.ClientSize.X, Game.Instance.ClientSize.Y);
        Editor = new Camera(new(10,10,10), Vector3.UnitY, Game.Instance.ClientSize.X, Game.Instance.ClientSize.Y);
        Editor.LookAt((0,0,0));
        Variables.VariableChanged += (s, e) =>
        {
            if (s == "fov")
            {
                Main.Fov = (float)e!;
            }
        };
    }

    public static Camera GetActiveCamera() => SceneEditor.Enabled ? Editor : Main;

    public void LookAt(Vector3 target)
    {
        Vector3 dir = Vector3.Normalize(target - Position);

        Pitch = MathHelper.RadiansToDegrees(MathF.Asin(dir.Y));
        Yaw = MathHelper.RadiansToDegrees(MathF.Atan2(dir.Z, dir.X));
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

    private void UpdateVectors()
    {
        Vector3 front;
        front.X = MathF.Cos(MathHelper.DegreesToRadians(Yaw)) *
                  MathF.Cos(MathHelper.DegreesToRadians(Pitch));
        front.Y = MathF.Sin(MathHelper.DegreesToRadians(Pitch));
        front.Z = MathF.Sin(MathHelper.DegreesToRadians(Yaw)) *
                  MathF.Cos(MathHelper.DegreesToRadians(Pitch));

        Front = Vector3.Normalize(front);
    }
}