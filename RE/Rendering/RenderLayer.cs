using OpenTK.Windowing.Common;

namespace RE.Rendering;

public enum RenderLayer
{
    Back,
    Skybox,
    World,
    UI,
    Overlay
}

public class RenderLayerManager
{
    public static SortedDictionary<RenderLayer, Dictionary<Type, List<IRenderable>>> Renderables = new();
    public static Dictionary<Type, Action> RenderablesInitActions = new();


    public static void Init()
    {
        Renderables.Clear();
        foreach (RenderLayer layer in Enum.GetValues(typeof(RenderLayer)))
            Renderables[layer] = new Dictionary<Type, List<IRenderable>>();
    }

    public static void AddRenderable(IRenderable renderable, Type type)
    {
        var types = Renderables[renderable.RenderLayer];
        if (!types.ContainsKey(type))
            types[type] = new List<IRenderable>();
        types[type].Add(renderable);
    }

    public static void RemoveRenderable(IRenderable renderable, Type type)
    {
        if (Renderables.TryGetValue(renderable.RenderLayer, out var types) && types.TryGetValue(type, out var list))
        {
            list.Remove(renderable);
            if (list.Count == 0)
                types.Remove(type);
        }
    }

    public static void RemoveRenderables(Type type)
    {
        foreach (var layer in Renderables.Values)
            if (layer.ContainsKey(type))
                layer[type].Clear();
    }

    public static void AddRenderableInitAction(Type type, Action action)
    {
        if (!RenderablesInitActions.TryAdd(type, action))
            RenderablesInitActions[type] += action;
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

    public static void RenderAll(FrameEventArgs args)
    {
        foreach (var kvp in Renderables)
        {
            var layer = kvp.Key;
            OnLayerBegin(layer);
            foreach (var VARIABLE in kvp.Value)
            {
                List<IRenderable> list = VARIABLE.Value;
                if (RenderablesInitActions.TryGetValue(VARIABLE.Key, out var value))
                    value.Invoke();

                foreach (var renderable in list)
                    if (renderable.IsVisible)
                        renderable.Render(args);
            }

            OnLayerEnd(layer);
        }
    }

    private static void OnLayerBegin(RenderLayer layer)
    {
        return;
        switch (layer)
        {
            case RenderLayer.UI:
                break;
        }
    }

    private static void OnLayerEnd(RenderLayer layer)
    {
    }
}