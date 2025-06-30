using OpenTK.Windowing.Common;
using RE.Core;
using RE.Libs.Grille.ImGuiTK;

namespace RE.Rendering;

public enum RenderLayer
{
    Back,
    Skybox,
    World,
    UI,
    Overlay,
    ImGui
}

public class RenderLayerManager
{
    public static SortedDictionary<RenderLayer, Dictionary<Type, List<IRenderable>>> Renderables = new();
    public static Dictionary<Type, Action> RenderablesInitActions = new();
    public static Dictionary<Type, Action> RenderablesPostActions = new();


    public static void Init()
    {
        Renderables.Clear();
        foreach (RenderLayer layer in Enum.GetValues(typeof(RenderLayer)))
            Renderables[layer] = new Dictionary<Type, List<IRenderable>>();
    }
    public static void AddRenderable<T>(T renderable) where T : IRenderable
    {
        var types = Renderables[renderable.RenderLayer];
        if (!types.ContainsKey(typeof(T)))
            types[typeof(T)] = new List<IRenderable>();
        types[typeof(T)].Add(renderable);
    }

    public static void RemoveRenderable<T>(T renderable) where T : IRenderable
    {
        if (Renderables.TryGetValue(renderable.RenderLayer, out var types) && types.TryGetValue(typeof(T), out var list))
        {
            list.Remove(renderable);
            if (list.Count == 0)
                types.Remove(typeof(T));
        }
    }

    public static void RemoveRenderables<T>() where T : IRenderable
    {
        foreach (var layer in Renderables.Values)
            if (layer.ContainsKey(typeof(T)))
                layer[typeof(T)].Clear();
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
    public static void RenderAll(FrameEventArgs args)
    {
        foreach (var kvp in Renderables)
        {
            var layer = kvp.Key;
            OnLayerBegin(layer);
            foreach (var pair in kvp.Value)
            {
                List<IRenderable> list = pair.Value;
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