using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using RE.Core;
using RE.Libs.Grille.ImGuiTK;
using System.Numerics;
using Vector3 = System.Numerics.Vector3;

namespace RE.Rendering;

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

    public static void Init()
    {
        Renderables.Clear();
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
    public static void RenderType<T>(T renderable, FrameEventArgs args) where T : IRenderable
    {
        if (Renderables.TryGetValue(renderable.RenderLayer, out var types) && types.TryGetValue(typeof(T), out var list))
        {
            if (RenderablesInitActions.TryGetValue(typeof(T), out var init))
                init.Invoke();
            foreach (var r in list)
                if (r.IsVisible)
                    r.Render(args);
            if (RenderablesPostActions.TryGetValue(typeof(T), out var post))
                post.Invoke();
        }
    }
    public static bool IsSphereInFrustum(Vector3 center, float radius)
    {
        foreach (var plane in frustumPlanes)
        {
            float distance = Vector3.Dot(plane.Normal, center) + plane.D;
            if (distance < -radius) // Сфера полностью за плоскостью — не видна
                return false;
        }
        return true;
    }

    public static Plane[] frustumPlanes = new Plane[6];
    public static void RenderAll(FrameEventArgs args)
    {
        // todo: add ICullable interface for Renderables
        #region Frustum culling
        Matrix4 view = Camera.Instance.GetViewMatrix();
        Matrix4 proj = Camera.Instance.GetProjectionMatrix();
        Matrix4 vp = view * proj;

        frustumPlanes[0] = new Plane( // Left
            vp.M14 + vp.M11,
            vp.M24 + vp.M21,
            vp.M34 + vp.M31,
            vp.M44 + vp.M41);
        frustumPlanes[1] = new Plane( // Right
            vp.M14 - vp.M11,
            vp.M24 - vp.M21,
            vp.M34 - vp.M31,
            vp.M44 - vp.M41);
        frustumPlanes[2] = new Plane( // Bottom
            vp.M14 + vp.M12,
            vp.M24 + vp.M22,
            vp.M34 + vp.M32,
            vp.M44 + vp.M42);
        frustumPlanes[3] = new Plane( // Top
            vp.M14 - vp.M12,
            vp.M24 - vp.M22,
            vp.M34 - vp.M32,
            vp.M44 - vp.M42);
        frustumPlanes[4] = new Plane( // Near
            vp.M13,
            vp.M23,
            vp.M33,
            vp.M43);
        frustumPlanes[5] = new Plane( // Far
            vp.M14 - vp.M13,
            vp.M24 - vp.M23,
            vp.M34 - vp.M33,
            vp.M44 - vp.M43);

        for (int i = 0; i < 6; i++)
        {
            float length = frustumPlanes[i].Normal.Length();
            frustumPlanes[i].Normal /= length;
            frustumPlanes[i].D /= length;
        }
        #endregion

        foreach (var kvp in Renderables)
        {
            var layer = kvp.Key;
            OnLayerBegin(layer);
            foreach (var pair in kvp.Value)
            {

                List<Renderable> list = pair.Value;
                if (RenderablesInitActions.TryGetValue(pair.Key, out var init))
                    init.Invoke();

                foreach (var renderable in list)
                    if (renderable.IsVisible)
                        renderable.Render(args);

                if (RenderablesPostActions.TryGetValue(pair.Key, out var post))
                    post.Invoke();
            }
            OnLayerEnd(layer);
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
}