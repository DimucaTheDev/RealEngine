using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core;
using RE.Core.Scripting;
using RE.Debug;
using RE.Editor;
using RE.Utils;
using Plane = System.Numerics.Plane;
using Quaternion = OpenTK.Mathematics.Quaternion;
using Vector3 = OpenTK.Mathematics.Vector3;
using Vector4 = OpenTK.Mathematics.Vector4;

namespace RE.Rendering;

//todo: move
public enum RenderLayer
{
    //make layers be able to be added from code
    Back,
    Skybox,
    World,
    UI,
    Overlay,
    ImGui
}

//todo перепиши весь класс 🙏😭

public class RenderManager
{
    public static SortedDictionary<RenderLayer, Dictionary<Type, List<Renderable>>> Renderables = new();
    public static Plane[] FrustumPlanes = new Plane[6];
    public static LineRenderer FrustumRenderer = new();
    public static List<Component> RenderingComponents = [];

    private static bool _hasCameraFrustum = false;
    private static Matrix4 _cachedViewMatrix, _cachedProjMatrix;

    public static void Init()
    {
        Renderables.Clear();
        FrustumRenderer.Clear();
        Initializer.InitializationCompleted += () => FrustumRenderer.StartRender();
        foreach (RenderLayer layer in Enum.GetValues(typeof(RenderLayer)))
            Renderables[layer] = new Dictionary<Type, List<Renderable>>();
    }
    public static void AddRenderable<T>(T renderable) where T : Renderable
    {
        var types = Renderables[renderable.RenderLayer];
        if (!types.ContainsKey(typeof(T)))
            types[typeof(T)] = new List<Renderable>();
        if (!types[typeof(T)].Contains(renderable))
        {
            types[typeof(T)].Add(renderable);
            renderable.AddedToRenderList();
        }
    }
    public static void RemoveRenderable<T>(T renderable) where T : Renderable
    {
        if (Renderables.TryGetValue(renderable.RenderLayer, out var types) && types.TryGetValue(typeof(T), out var list))
        {
            list.Remove(renderable);
            if (list.Count == 0)
            {
                types.Remove(typeof(T));
                renderable.RemovedFromRenderList();
            }
        }
    }
    public static void RenderType<T>(T renderable, FrameEventArgs args) where T : Renderable
    {
        //todo: GenerateFrustum() should be called before this method
        if (Renderables.TryGetValue(renderable.RenderLayer, out var types) && types.TryGetValue(typeof(T), out var list))
        {
            foreach (var r in list)
            {
                if (r.IsVisible)
                {
                    if (r is ICullable { ShouldCull: true } cull)
                    {
                        if (!IsObbInFrustum(cull.Position, cull.Scale, cull.Rotation))
                        {
                            continue;
                        }
                    }
                    r.Render(args);
                }
            }
        }
    }


    //todo: fixme
    public static bool IsObbInFrustum(Vector3 position, Vector3 scale, Quaternion rotation)
    {
        return true; // doesnt work
#pragma warning disable CS0162 
        var localCorners = new Vector3[]
        {
            new(-0.5f, -0.5f, -0.5f),
            new(-0.5f, -0.5f,  0.5f),
            new(-0.5f,  0.5f, -0.5f),
            new(-0.5f,  0.5f,  0.5f),
            new( 0.5f, -0.5f, -0.5f),
            new( 0.5f, -0.5f,  0.5f),
            new( 0.5f,  0.5f, -0.5f),
            new( 0.5f,  0.5f,  0.5f),
        };

        Matrix4 model = Matrix4.CreateScale(scale) *
                        Matrix4.CreateFromQuaternion(rotation) *
                        Matrix4.CreateTranslation(position);

        Matrix4 mvp;
        if (_hasCameraFrustum)
            mvp = model * _cachedViewMatrix * _cachedProjMatrix;
        else
            mvp = model * Camera.GetActiveCamera().GetViewMatrix() * Camera.GetActiveCamera().GetProjectionMatrix(120);

        foreach (var corner in localCorners)
        {
            Vector4 clip = Vector4.TransformRow(new Vector4(corner, 1.0f), mvp);

            if (clip.W <= 0)
                continue;

            Vector3 ndc = new Vector3(clip.X, clip.Y, clip.Z) / clip.W;

            if (ndc.X >= -1 && ndc.X <= 1 &&
                ndc.Y >= -1 && ndc.Y <= 1 &&
                ndc.Z >= 0 && ndc.Z <= 1)
            {
                return true;
            }
        }
        return false;
    }

    public static void RenderAll(FrameEventArgs args)
    {
        GenerateFrustum();

        var camPos = Camera.GetActiveCamera().Position;
        RenderingComponents.Sort((a, b) =>
        {
            float da = (a.Owner.Transform.Position - camPos).LengthSquared;
            float db = (b.Owner.Transform.Position - camPos).LengthSquared;
            return db.CompareTo(da);
        });

        if (!SceneEditor.Enabled)
            foreach (var s in RenderingComponents.ToList())
            {
                s.Render(args);
            }

        foreach (var kvp in Renderables)
        {
            foreach (var pair in kvp.Value)
            {
                //todo: move to RenderType()

                List<Renderable> list = pair.Value;

                foreach (var renderable in list)
                    if (renderable.IsVisible)
                    {
                        if (renderable is ICullable { ShouldCull: true } cull)
                        {
                            if (!IsObbInFrustum(cull.Position, cull.Scale, cull.Rotation))
                            {
                                continue;
                            }
                        }

                        renderable.Render(args);
                    }

            }
        }
    }

    private static void GenerateFrustum()
    {
        Matrix4 view = Camera.GetActiveCamera().GetViewMatrix();
        Matrix4 proj = Camera.GetActiveCamera().GetProjectionMatrix();
        Matrix4 vp = view * proj;

        FrustumPlanes[0] = new Plane( // Left
            vp.M14 + vp.M11,
            vp.M24 + vp.M21,
            vp.M34 + vp.M31,
            vp.M44 + vp.M41);
        FrustumPlanes[1] = new Plane( // Right
            vp.M14 - vp.M11,
            vp.M24 - vp.M21,
            vp.M34 - vp.M31,
            vp.M44 - vp.M41);
        FrustumPlanes[2] = new Plane( // Bottom
            vp.M14 + vp.M12,
            vp.M24 + vp.M22,
            vp.M34 + vp.M32,
            vp.M44 + vp.M42);
        FrustumPlanes[3] = new Plane( // Top
            vp.M14 - vp.M12,
            vp.M24 - vp.M22,
            vp.M34 - vp.M32,
            vp.M44 - vp.M42);
        FrustumPlanes[4] = new Plane( // Near
            vp.M13,
            vp.M23,
            vp.M33,
            vp.M43);
        FrustumPlanes[5] = new Plane( // Far
            vp.M14 - vp.M13,
            vp.M24 - vp.M23,
            vp.M34 - vp.M33,
            vp.M44 - vp.M43);

        for (int i = 0; i < 6; i++)
        {
            float length = FrustumPlanes[i].Normal.Length();
            FrustumPlanes[i].Normal /= length;
            FrustumPlanes[i].D /= length;
        }
    }

    //todo: move somewhere...
    //Debug
    public static void CreateCameraFrustum()
    {
        _hasCameraFrustum = true;
        _cachedViewMatrix = Camera.Main.GetViewMatrix();
        _cachedProjMatrix = Camera.Main.GetProjectionMatrix();
        FrustumRenderer.Clear();

        float r = .5f;

        var corners = GetFrustumCorners(_cachedProjMatrix, _cachedViewMatrix);
        FrustumRenderer.AddLine(corners[0], corners[1], new Vector4(r, 0, 0, 1), new Vector4(r, 0, 0, 1), 0);
        FrustumRenderer.AddLine(corners[1], corners[2], new Vector4(r, 0, 0, 1), new Vector4(r, 0, 0, 1), 0);
        FrustumRenderer.AddLine(corners[2], corners[3], new Vector4(r, 0, 0, 1), new Vector4(r, 0, 0, 1), 0);
        FrustumRenderer.AddLine(corners[3], corners[0], new Vector4(r, 0, 0, 1), new Vector4(r, 0, 0, 1), 0);
        FrustumRenderer.AddLine(corners[4], corners[5], new Vector4(r, 0, 0, 1), new Vector4(r, 0, 0, 1), 0);
        FrustumRenderer.AddLine(corners[5], corners[6], new Vector4(r, 0, 0, 1), new Vector4(r, 0, 0, 1), 0);
        FrustumRenderer.AddLine(corners[6], corners[7], new Vector4(r, 0, 0, 1), new Vector4(r, 0, 0, 1), 0);
        FrustumRenderer.AddLine(corners[7], corners[4], new Vector4(r, 0, 0, 1), new Vector4(r, 0, 0, 1), 0);

        for (int i = 0; i < 4; i++)
        {
            FrustumRenderer.AddLine(corners[i], corners[i + 4], new Vector4(r, 0, 0, 1), new Vector4(r, 0, 0, 1), 0);
        }
    }

    public static void DrawCameraFrustum()
    {
        _cachedViewMatrix = Camera.Main.GetViewMatrix();
        _cachedProjMatrix = Camera.Main.GetProjectionMatrix();

        Vector4 color = (0.3f, 0.3f, 0.3f, 1);

        var corners = GetFrustumCorners(_cachedProjMatrix, _cachedViewMatrix, 1.3f);
        LineRenderer.DrawLine(corners[0], corners[1], color, color);
        LineRenderer.DrawLine(corners[1], corners[2], color, color);
        LineRenderer.DrawLine(corners[2], corners[3], color, color);
        LineRenderer.DrawLine(corners[3], corners[0], color, color);
        LineRenderer.DrawLine(corners[4], corners[5], color, color);
        LineRenderer.DrawLine(corners[5], corners[6], color, color);
        LineRenderer.DrawLine(corners[6], corners[7], color, color);
        LineRenderer.DrawLine(corners[7], corners[4], color, color);

        for (int i = 0; i < 4; i++)
        {
            LineRenderer.DrawLine(corners[i], corners[i + 4], color, color);
        }
    }

    public static void RemoveCameraFrustum()
    {
        FrustumRenderer.Clear();
        _hasCameraFrustum = false;
    }
    private static Vector3[] GetFrustumCorners(
        Matrix4 proj,
        Matrix4 view,
        float maxDistance = 1000f)
    {
        Matrix4 inv = Matrix4.Invert(view * proj);
        Vector3 camPos = Camera.Main.Position;

        Vector3[] ndc =
        {
            new(-1, -1, -1),
            new( 1, -1, -1),
            new( 1,  1, -1),
            new(-1,  1, -1),

            new(-1, -1,  1),
            new( 1, -1,  1),
            new( 1,  1,  1),
            new(-1,  1,  1),
        };

        Vector3[] corners = new Vector3[8];

        for (int i = 0; i < 8; i++)
        {
            Vector4 clip = new Vector4(ndc[i], 1f);
            Vector4 world = Vector4.TransformRow(clip, inv);
            world /= world.W;

            Vector3 pos = world.Xyz;

            Vector3 dir = pos - camPos;
            float dist = dir.Length;

            if (dist > maxDistance)
            {
                pos = camPos + dir.Normalized() * maxDistance;
            }

            corners[i] = pos;
        }

        return corners;
    }

}