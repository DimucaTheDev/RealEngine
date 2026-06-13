using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using DotRecast.Core.Collections.Extensions;
using OpenTK.Mathematics;
using RE.Core.PluginSystem;
using RE.Editor.Panels.Viewport;
using RE.Utils;
using Serilog;

namespace RE.Core.World;

public static class SceneSerializer
{
    public static string SerializeScene(Scene scene)
    {
        var root = new JsonObject();
        var objects = new JsonArray();
        foreach (var obj in scene.GameObjects.Where(s => s is { DoNotSave: false, Parent: null }))
        {
            objects.Add(ConstructObject(obj));
            continue;

            static JsonObject ConstructObject(GameObject obj)
            {
                JsonObject o = new JsonObject { { "name", obj.Name }, { "tag", obj.Tag } };

                var transform = obj.Transform;
                JsonObject trObj = new()
                {
                    {
                        "position",
                        new JsonArray(transform.Position[0], transform.Position[1], transform.Position[2])
                    },
                    {
                        "rotation",
                        new JsonArray(MathHelper.RadiansToDegrees(transform.Rotation.X),
                            MathHelper.RadiansToDegrees(transform.Rotation.Y),
                            MathHelper.RadiansToDegrees(transform.Rotation.Z))
                    },
                    {
                        "scale",
                        new JsonArray(transform.Scale[0], transform.Scale[1], transform.Scale[2])
                    }
                };

                o.Add("transform", trObj);

                JsonObject componentsObj = new();
                o.Add("components", componentsObj);
                foreach (var com in obj.Components.Where(s => s.SaveComponent))
                {
                    var saveData = com.GetSaveData();
                    componentsObj.Add(com.GetType().Name, saveData);
                }

                JsonArray childArray = new JsonArray();
                obj.Children.ForEach(child => childArray.Add(ConstructObject(child)));
                o.Add("children", childArray);

                return o;
            }
        }

        root.Add("objects", objects);

        var jsonString = root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        });
        return jsonString;
    }

    private static void ConstructComponent(JsonProperty component, Type type, GameObject o)
    {
        object instance = CreateInstanceWithArgs(type, component.Value);
        Component c = (Component)instance;
        o.Components.Add(c, true);

        foreach (var prop in component.Value.EnumerateObject())
        {
            if (prop.Name.Equals("args", StringComparison.OrdinalIgnoreCase))
                continue;

            MemberInfo? member = type.GetProperty(prop.Name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (member == null)
            {
                member = type.GetField(prop.Name,
                             BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                             BindingFlags.IgnoreCase)
                         ?? type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                             .FirstOrDefault(f =>
                                 f.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                                     .Equals(prop.Name, StringComparison.OrdinalIgnoreCase) == true);
            }

            if (member == null)
            {
                Serilog.Log.Error("Property or Field {MemberName} was not found on type {TypeName}", prop.Name,
                    type.Name);
                continue;
            }

            Type targetType = member is PropertyInfo p ? p.PropertyType : ((FieldInfo)member).FieldType;
            object? deserializedValue = DeserializeMember(prop.Value, targetType);

            if (member is PropertyInfo propInfo && propInfo.CanWrite)
            {
                propInfo.SetValue(instance, deserializedValue);
            }
            else if (member is FieldInfo fieldInfo)
            {
                fieldInfo.SetValue(instance, deserializedValue);
            }
        }
    }

    private static object CreateInstanceWithArgs(Type type, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("args", out var argsElement) &&
            argsElement.ValueKind == JsonValueKind.Array)
        {
            var ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            ConstructorInfo? matchingCtor =
                ctors.FirstOrDefault(c => c.GetParameters().Length == argsElement.GetArrayLength());

            if (matchingCtor == null)
                throw new InvalidOperationException(
                    $"No constructor with {argsElement.GetArrayLength()} arguments found for {type.Name}");

            var paramInfos = matchingCtor.GetParameters();
            var parsedArgs = new object?[paramInfos.Length];

            for (int i = 0; i < paramInfos.Length; i++)
            {
                parsedArgs[i] = DeserializeMember(argsElement[i], paramInfos[i].ParameterType);
            }

            return Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                null, parsedArgs, null)!;
        }

        return Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            null, [], null)!;
    }

    private static object? DeserializeMember(JsonElement element, Type targetType)
    {
        if (element.ValueKind == JsonValueKind.Null) return null;

        if (targetType == typeof(Vector3))
        {
            if (element.ValueKind == JsonValueKind.Array && element.GetArrayLength() == 3)
            {
                var arr = element.EnumerateArray().Select(s => s.GetSingle()).ToArray();
                return new Vector3(arr[0], arr[1], arr[2]);
            }

            return Vector3.Zero;
        }

        if (targetType.GetCustomAttribute<SerializerCallsConstructorAttribute>() != null)
        {
            object subInstance = CreateInstanceWithArgs(targetType, element);

            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.Name.Equals("args", StringComparison.OrdinalIgnoreCase)) continue;

                    var propInfo = targetType.GetProperty(prop.Name,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (propInfo != null && propInfo.CanWrite)
                    {
                        propInfo.SetValue(subInstance, DeserializeMember(prop.Value, propInfo.PropertyType));
                    }
                }
            }

            return subInstance;
        }
 
        if (targetType.IsGenericType && (targetType.GetGenericTypeDefinition() == typeof(Dictionary<,>) ||
                                         targetType.GetGenericTypeDefinition() == typeof(IDictionary<,>)))
        {
            var keyType = targetType.GetGenericArguments()[0];
            var valueType = targetType.GetGenericArguments()[1];

            var dict = (IDictionary)Activator.CreateInstance(
                typeof(Dictionary<,>).MakeGenericType(keyType, valueType))!;

            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in element.EnumerateObject())
                {
                    var key = Convert.ChangeType(prop.Name, keyType);
                    var val = DeserializeMember(prop.Value, valueType);
                    dict.Add(key, val);
                }
            }
            else if
                (element.ValueKind ==
                 JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    var val = DeserializeMember(item, valueType);
                    if (val != null)
                    {
                        var nameProp = valueType.GetProperty("Name") ??
                                       throw new InvalidOperationException(
                                           "JsonArray elements for Dictionary must have a 'Name' property.");
                        var key = nameProp.GetValue(val);
                        if (key != null) dict.Add(key, val);
                    }
                }
            }

            return dict;
        }

        return JsonSerializer.Deserialize(element.GetRawText(), targetType);
    }

    public static Scene DeserializeScene(string jsonString, string? name = null)
    {
        var temp = SceneManager.CurrentScene;
        try
        {
            var assemblyTypes = new[] { Assembly.GetExecutingAssembly() }
                .Concat(PluginManager.LoadedPlugins.Select(s => s.PluginInformation.Assembly))
                .SelectMany(assembly => assembly.GetTypes())
                .Distinct()
                .Where(type => typeof(Component).IsAssignableFrom(type) && !type.IsAbstract)
                .ToList();

            JsonDocument doc = JsonDocument.Parse(jsonString,
                new JsonDocumentOptions
                    { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

            Scene scene = new Scene();
            SceneManager.CurrentScene = scene;

            var hasSceneName = doc.RootElement.TryGetProperty("name", out var sceneName);

            scene.Name = name ?? (hasSceneName ? sceneName.GetString() : "Unnamed");


            if (!(doc.RootElement.TryGetProperty("objects", out var objProp) && objProp.EnumerateArray().Any()))
            {
                Log.Warning("Scene does not contain any objects.");
            }

            var objects = objProp.EnumerateArray();

            foreach (var obj in objects)
            {
                scene.GameObjects.Add(ProcessObjects(obj), true);

                GameObject ProcessObjects(JsonElement @object)
                {
                    GameObject gameObject = new GameObject();
                    gameObject.Scene = scene;

                    gameObject.Name =
                        @object.TryGetProperty("name", out JsonElement nameProperty)
                            ? nameProperty.GetString()!
                            : Random.Shared.Next().ToString();

                    gameObject.Tag =
                        @object.TryGetProperty("tag", out JsonElement tagProperty)
                            ? tagProperty.GetString()!
                            : null;

                    if (@object.TryGetProperty("transform", out var transformElement))
                    {
                        if (transformElement.TryGetProperty("position", out var positionElement))
                        {
                            var array = positionElement.EnumerateArray().Select(s => s.GetSingle()).ToList();
                            gameObject.Transform.Position = new Vector3(array[0], array[1], array[2]);
                        }

                        if (transformElement.TryGetProperty("rotation", out var rotationElement))
                        {
                            var array = rotationElement.EnumerateArray().Select(s => s.GetSingle()).ToArray();
                            gameObject.Transform.RotationXyz = new(array[0], array[1], array[2]);
                        }

                        if (transformElement.TryGetProperty("scale", out var scaleElement))
                        {
                            var array = scaleElement.EnumerateArray().Select(s => s.GetSingle()).ToList();
                            gameObject.Transform.Scale = new Vector3(array[0], array[1], array[2]);
                        }
                    }

                    if (@object.TryGetProperty("components", out var components))
                    {
                        var componentList = components.EnumerateObject().ToList();

                        HashSet<string> existingComponents = componentList
                            .Select(c => c.Name.ToLower().Replace("component", ""))
                            .ToHashSet();

                        List<Type> additionalComponents = [];

                        foreach (var component in componentList)
                        {
                            var type = assemblyTypes.FirstOrDefault(s =>
                                s.Name.ToLower().Replace("component", "") ==
                                component.Name.ToLower().Replace("component", ""));

                            if (type == null)
                                continue;

                            foreach (var requiredType in GetRequiredComponents(type, gameObject))
                            {
                                var normalizedName = requiredType.Name
                                    .ToLower()
                                    .Replace("component", "");

                                if (existingComponents.Add(normalizedName))
                                {
                                    additionalComponents.Add(requiredType);
                                }
                            }
                        }

                        foreach (var type in additionalComponents)
                        {
                            try
                            {
                                Component c = (Component)Activator.CreateInstance(
                                    type,
                                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                                    binder: null,
                                    args: [],
                                    culture: null)!;

                                gameObject.Components.Add(c, true);
                                Log.Verbose("Added {Name} to {Object}", type.Name, gameObject.Name);
                            }
                            catch (Exception e)
                            {
                                Log.Error(
                                    e,
                                    "Failed to create required component {ComponentName} on object {ObjectName}",
                                    type.Name,
                                    gameObject.Name);
                                throw;
                            }
                        }

                        foreach (var component in componentList)
                        {
                            var type = assemblyTypes.FirstOrDefault(s =>
                                s.Name.ToLower().Replace("component", "") ==
                                component.Name.ToLower().Replace("component", ""));

                            if (type == null)
                            {
                                Log.Error(
                                    "Unknown component {ComponentName} on object {ObjectName} in {SceneName}",
                                    component.Name,
                                    gameObject.Name,
                                    scene.Name);

                                continue;
                            }

                            ConstructComponent(component, type, gameObject);
                        }
                    }

                    if (@object.TryGetProperty("children", out var children))
                    {
                        foreach (var child in children.EnumerateArray())
                        {
                            var childObject = ProcessObjects(child);
                            childObject.Parent = gameObject;
                            scene.GameObjects.Add(childObject);
                        }
                    }

                    return gameObject;
                }
            }

            ApplyObjectModification();

            foreach (var go in scene.GameObjects.ToList())
            {
                foreach (var c in go.Components.ToList())
                {
                    c.Start();
                }
            }

            return scene;
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to deserialize scene");
            throw;
        }
        finally
        {
            SceneManager.CurrentScene = temp;
        }
    }

    private static IEnumerable<Type> GetRequiredComponents(Type type, GameObject o)
    {
        HashSet<Type> result = [];

        while (type != null &&
               type != typeof(object) &&
               type != typeof(Component))
        {
            var attrs = type.GetRequiredComponents();

            foreach (var attr in attrs)
            {
                if (result.Add(attr))
                {
                    Log.Warning(
                        "Added component {RequiredComponentName} because {ComponentName} on {ObjectName}({ObjectId}) requires it",
                        attr.Name, type.Name, o.Name, o.Id);
                }
            }

            type = type.BaseType!;
        }

        return result;
    }

    internal static void ApplyObjectModification()
    {
        if (SceneManager.CurrentScene == null!) return;
        SceneManager.CurrentScene.GameObjects._objects.AddRange(SceneManager._objectsToAdd);

        foreach (var g in SceneManager._objectsToRemove)
        {
            for (var i = g.Components.Count - 1; i >= 0; i--)
            {
                g.Components.Remove(g.Components[i]);
            }

            SceneManager.CurrentScene.GameObjects._objects.Remove(g);
            ViewportPanel.CollisionWorld.RemoveCollisionObject(g.ViewportObject);
            g.ViewportObject.Dispose();
        }

        SceneManager._objectsToAdd.Clear();
        SceneManager._objectsToRemove.Clear();
    }
}