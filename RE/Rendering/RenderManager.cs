using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core;
using RE.Debug;
using RE.Libs.Grille.ImGuiTK;
using RE.Utils;
using System.Numerics;
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

public class RenderManager
{
    public static SortedDictionary<RenderLayer, Dictionary<Type, List<Renderable>>> Renderables = new();
    public static Dictionary<Type, Action> RenderablesInitActions = new();
    public static Dictionary<Type, Action> RenderablesPostActions = new();
    public static Plane[] FrustumPlanes = new Plane[6];
    public static LineManager FrustumRenderer = new();

    private static bool _hasCameraFrustum = false;
    private static Matrix4 _cachedViewMatrix, _cachedProjMatrix;


    public static void Init()
    {
        Renderables.Clear();
        FrustumRenderer.Init();
        FrustumRenderer.Clear();
        Initializer.InitializationCompleted += () => FrustumRenderer.Render();
        foreach (RenderLayer layer in Enum.GetValues(typeof(RenderLayer)))
            Renderables[layer] = new Dictionary<Type, List<Renderable>>();
    }
    #region Bleh
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
    public static void RemoveRenderables<T>() where T : Renderable
    {
        foreach (var layer in Renderables.Values)
            if (layer.ContainsKey(typeof(T)))
            {
                layer[typeof(T)].ForEach(r => r.RemovedFromRenderList());
                layer[typeof(T)].Clear();
            }
    }
    public static void SetRenderableInitAction<T>(Action action)
    {
        if (!RenderablesInitActions.TryAdd(typeof(T), action))
            RenderablesInitActions[typeof(T)] += action;
    }
    public static void SetRenderablePostAction<T>(Action action)
    {
        if (!RenderablesPostActions.TryAdd(typeof(T), action))
            RenderablesPostActions[typeof(T)] += action;
    }
    public static void RemoveRenderableInitAction(Type type, Action action)
    {
        if (RenderablesInitActions.TryGetValue(type, out var existingAction))
        {
            existingAction -= action;
            if (existingAction == null)
                RenderablesInitActions.Remove(type);
            else
                RenderablesInitActions[type] = existingAction;
        }
    }
    public static void RemoveRenderablePostAction(Type type, Action action)
    {
        if (RenderablesPostActions.TryGetValue(type, out var existingAction))
        {
            existingAction -= action;
            if (existingAction == null)
                RenderablesPostActions.Remove(type);
            else
                RenderablesPostActions[type] = existingAction;
        }
    }
    #endregion
    public static void RenderType<T>(T renderable, FrameEventArgs args) where T : IRenderable
    {
        //todo: GenerateFrustum() should be called before this method
        if (Renderables.TryGetValue(renderable.RenderLayer, out var types) && types.TryGetValue(typeof(T), out var list))
        {
            if (RenderablesInitActions.TryGetValue(typeof(T), out var init))
                init.Invoke();
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
            if (RenderablesPostActions.TryGetValue(typeof(T), out var post))
                post.Invoke();
        }
    }


    //todo: fixme
    public static bool IsObbInFrustum(Vector3 position, Vector3 scale, Quaternion rotation)
    {
        return true; // doesnt work

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
            mvp = model * Camera.Instance.GetViewMatrix() * Camera.Instance.GetProjectionMatrix(120);

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
        foreach (var kvp in Renderables)
        {
            var layer = kvp.Key;
            OnLayerBegin(layer);
            foreach (var pair in kvp.Value)
            {
                //todo: move to RenderType()

                List<Renderable> list = pair.Value;
                if (RenderablesInitActions.TryGetValue(pair.Key, out var init))
                    init.Invoke();

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

                if (RenderablesPostActions.TryGetValue(pair.Key, out var post))
                    post.Invoke();
            }
            OnLayerEnd(layer);
        }
    }

    private static void GenerateFrustum()
    {
        Matrix4 view = Camera.Instance.GetViewMatrix();
        Matrix4 proj = Camera.Instance.GetProjectionMatrix();
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
    private static void OnLayerBegin(RenderLayer layer)
    {
        switch (layer)
        {
            case RenderLayer.ImGui:

                ImGuiController.Get().Update(Game.Instance, Time.DeltaTime);
                break;
        }
    }

    private static void OnLayerEnd(RenderLayer layer)
    {
        switch (layer)
        {
            case RenderLayer.ImGui:
                ImGuiController.Get().Render();
                break;
        }
    }

    //todo: move somewhere...
    //Debug
    public static void CreateCameraFrustum()
    {
        _hasCameraFrustum = true;
        _cachedViewMatrix = Camera.Instance.GetViewMatrix();
        _cachedProjMatrix = Camera.Instance.GetProjectionMatrix(120);
        FrustumRenderer.Clear();

        float r = .1f;

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

        r = 1f;
        corners = GetFrustumCorners(Camera.Instance.GetProjectionMatrix(), _cachedViewMatrix);
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

    public static void RemoveCameraFrustum()
    {
        FrustumRenderer.Clear();
        _hasCameraFrustum = false;
    }
    private static Vector3[] GetFrustumCorners(Matrix4 proj, Matrix4 view, float maxDistance = 1000f)
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

}