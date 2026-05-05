using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using OpenTK.Mathematics;
using RE.Core.Assets;
using RE.Core.Initializing;
using RE.Core.PluginSystem;
using RE.Editor.Panels.Viewport;
using Serilog;

namespace RE.Core.World
{
    /// <summary>
    /// Static class that manages loading, saving, and transitioning between scenes.
    /// </summary>
    public static class SceneManager
    {
        internal static readonly List<GameObject> _objectsToAdd = new();
        internal static readonly List<GameObject> _objectsToRemove = new();

        internal static bool SceneChanged
        {
            get;
            set
            {
                field = value;
                //Log.Debug("Set {Property} to {Value}", nameof(SceneChanged), value);
            }
        }

        /// <summary>
        /// Currently loaded and active scene.
        /// </summary>
        public static Scene CurrentScene { get; internal set; } = null!;

        /// <summary>
        /// Transitions to a new scene, disposing of the current one.
        /// </summary>
        /// <param name="scene">New scene to be loaded</param>
        public static void LoadScene(Scene scene) => LoadScene(scene, true, null);

        /// <summary>
        /// Transitions to a new scene, optionally disposing of the current one.
        /// </summary>
        /// <param name="scene">New scene to be loaded</param>
        /// <param name="disposeCurrent">Whether to dispose currently loaded scene or not</param>
        public static void LoadScene(Scene scene, bool disposeCurrent) => LoadScene(scene, disposeCurrent, null);

        /// <summary>
        /// Transitions to a new scene, optionally disposing of the current one, and invoking action after loading.
        /// </summary>
        /// <param name="scene">New scene to be loaded</param>
        /// <param name="disposeCurrent">Whether to dispose currently loaded scene or not</param>
        /// <param name="afterLoaded">Invoke action when scene finishes loading</param>
        public static void LoadScene(Scene scene, bool disposeCurrent, Action? afterLoaded)
        {
            /*Initializer.AddStep(new InitializingTask
            {
                Label = $"Loading level \"{scene.Name ?? "<unnamed>"}\"",
                Action = () =>
                {*/
            SceneChanged = true;

            //Hud.Root.Children.Clear();
            //Log.Debug("Scene changed, HUD canvas cleared");

            if (scene == CurrentScene)
            {
                CurrentScene = Reload(scene);
                scene.Dispose();
            }
            else
            {
                if (CurrentScene != null! && disposeCurrent)
                {
                    CurrentScene.Dispose();
                }

                CurrentScene = scene;

                if (!CurrentScene.LightSources.Any())
                    Log.Warning("No light sources in {SceneName}", CurrentScene.Name);
            }

            afterLoaded?.Invoke();
            /*}
        });*/
        }

        /// <summary>
        /// Saves the provided scene to the specified path in JSON format.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The path can be either a directory or a file path ending with <c>.json</c>.<br/>
        /// If a directory is provided, the scene will be saved as <c>data.json</c> within that directory.
        /// </para>
        /// </remarks>
        /// <exception cref="JsonException">Can occur if data saved via <see cref="Component.GetSaveData"/> can not be converted to JSON</exception>
        /// <param name="scene">Scene to be saved</param>
        /// <param name="path">
        /// Path where save file will be created.
        /// <remarks>
        /// <para>If path ends with <c>.json</c>, the file will be created at that path, otherwise a <c>data.json</c> will be created at specified path</para>
        /// </remarks>
        /// </param>
        public static void SaveSceneToFile(Scene scene, string path)
        {
            var jsonString = SerializeScene(scene);

            string savedTo;
            if (jsonString.EndsWith(".json")) // a file
            {
                var directoryName = Path.GetDirectoryName(path);
                if (!Directory.Exists(directoryName))
                    Directory.CreateDirectory(directoryName!);
                File.WriteAllText(savedTo = path, jsonString);
            }
            else
            {
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                File.WriteAllText(savedTo = Path.Combine(path, "data.json"), jsonString);
            }

            Log.Information("Saved level to '{Path}'", savedTo);
        }

        public static string SerializeScene(Scene scene)
        {
            var root = new JsonObject();
            var objects = new JsonArray();
            foreach (var obj in scene.GameObjects.Where(s => !s.DoNotSave))
            {
                JsonObject o = new JsonObject { { "name", obj.Name }, { "tag", obj.Tag } };
                objects.Add(o);
                var transform = obj.Transform;
                JsonObject trObj = new()
                {
                    { "position", new JsonArray(transform.Position[0], transform.Position[1], transform.Position[2]) },
                    {
                        "rotation",
                        new JsonArray(MathHelper.RadiansToDegrees(transform.Rotation.X),
                            MathHelper.RadiansToDegrees(transform.Rotation.Y),
                            MathHelper.RadiansToDegrees(transform.Rotation.Z))
                    },
                    { "scale", new JsonArray(transform.Scale[0], transform.Scale[1], transform.Scale[2]) }
                };
                o.Add("transform", trObj);
                JsonObject componentsObj = new();
                o.Add("components", componentsObj);
                foreach (var com in obj.Components.Where(s => s.SaveComponent))
                {
                    var saveData = com.GetSaveData();
                    componentsObj.Add(com.GetType().Name, saveData);
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


        //todo: parse at PATH, not just name
        /// <summary>
        /// Loads and deserializes a scene from JSON file located at <c>Assets/Maps/{name}/data.json</c>.
        /// </summary>
        /// <param name="name">Scene name in <c>Assets/Maps</c></param>
        /// <returns>A new scene instance</returns>
        public static Scene ParseScene(string name)
        {
            string dataPath = Path.Combine("Assets", "Maps", name, "data.json");

            return DeserializeScene(ContentManager.GetString(dataPath), name);
        }

        public static Scene DeserializeScene(string jsonString, string? name = null)
        {
            var temp = CurrentScene; // КОСТЫЛЬ
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
                CurrentScene = scene;

                var hasSceneName = doc.RootElement.TryGetProperty("name", out var sceneName);

                scene.Name = name ?? (hasSceneName ? sceneName.GetString() : "Unnamed"); //TODO: get name from manifest


                if (!(doc.RootElement.TryGetProperty("objects", out var objProp) && objProp.EnumerateArray().Any()))
                {
                    Log.Warning("Scene does not contain any objects.");
                }

                var objects = objProp.EnumerateArray();

                foreach (var obj in objects)
                {
                    GameObject gameObject = new GameObject();
                    gameObject.Scene = scene;

                    gameObject.Name =
                        obj.TryGetProperty("name", out JsonElement nameProperty)
                            ? nameProperty.GetString()!
                            : Random.Shared.Next().ToString();

                    gameObject.Tag =
                        obj.TryGetProperty("tag", out JsonElement tagProperty)
                            ? tagProperty.GetString()!
                            : null;

                    if (obj.TryGetProperty("transform", out var transformElement))
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

                    if (obj.TryGetProperty("components", out var components))
                    {
                        foreach (var component in components.EnumerateObject())
                        {
                            var type = assemblyTypes
                                .FirstOrDefault(s => s.Name.ToLower().Replace("component", "") ==
                                                     component.Name.ToLower().Replace("component", ""));

                            if (type == null)
                            {
                                Log.Error("Unknown component {ComponentName} on object {ObjectName} in {SceneName}",
                                    component.Name, gameObject.Name, scene.Name);
                                continue;
                            }

                            object instance;

                            if (component.Value.TryGetProperty("args", out var argsElement) &&
                                argsElement.ValueKind == JsonValueKind.Array)
                            {
                                var ctors = type.GetConstructors();
                                ConstructorInfo? matchingCtor = ctors.FirstOrDefault(c =>
                                    c.GetParameters().Length == argsElement.GetArrayLength());

                                if (matchingCtor == null)
                                    throw new InvalidOperationException(
                                        $"No constructor with {argsElement.GetArrayLength()} arguments found for {type.Name}");

                                var paramInfos = matchingCtor.GetParameters();
                                var parsedArgs = new object?[paramInfos.Length];

                                for (int i = 0; i < paramInfos.Length; i++)
                                {
                                    var paramType = paramInfos[i].ParameterType;
                                    var argElement = argsElement[i];

                                    parsedArgs[i] = JsonSerializer.Deserialize(argElement.GetRawText(), paramType)
                                                    ?? throw new InvalidOperationException(
                                                        $"Failed to deserialize argument {i} to {paramType}");
                                }

                                instance = Activator.CreateInstance(type, parsedArgs)!;
                            }
                            else
                                instance = Activator.CreateInstance(type)!;

                            Component c = (Component)instance;
                            gameObject.Components.Add(c, true);

                            foreach (var prop in component.Value.EnumerateObject())
                            {
                                if (prop.Name.ToLower() == "args")
                                    continue;

                                var propertyName = prop.Name;
                                var propertyInfo = type.GetProperty(propertyName,
                                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                                if (propertyInfo == null)
                                {
                                    Log.Error("Property '{PropertyName}' not found on type '{TypeName}'", propertyName,
                                        type.Name);
                                    continue;
                                }

                                object? propertyValue = null!;
                                if (propertyInfo!.PropertyType == typeof(Vector3)) //float[] -> Vec3(xyz)
                                {
                                    var arr = prop.Value.EnumerateArray().Select(s => s.GetSingle()).ToList();
                                    propertyValue = new Vector3(arr[0], arr[1], arr[2]);
                                }
                                else
                                    propertyValue = prop.Value.Deserialize(propertyInfo!.PropertyType);

                                if (propertyInfo != null! && propertyInfo.CanWrite)
                                {
                                    propertyInfo.SetValue(instance, propertyValue);
                                }
                            }
                        }
                    }

                    scene.GameObjects.Add(gameObject, true);
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
                CurrentScene = temp;
            }
        }

        public static Scene Reload(Scene scene)
        {
            var newScene = DeserializeScene(SerializeScene(scene), scene.Name);
            return newScene;
        }

        internal static void ApplyObjectModification()
        {
            if (CurrentScene == null!) return;
            CurrentScene.GameObjects._objects.AddRange(_objectsToAdd);

            foreach (var g in _objectsToRemove)
            {
                for (var i = g.Components.Count - 1; i >= 0; i--)
                {
                    g.Components.Remove(g.Components[i]);
                }

                CurrentScene.GameObjects._objects.Remove(g);
                ViewportPanel.CollisionWorld.RemoveCollisionObject(g.ViewportObject);
                g.ViewportObject.Dispose();
            }

            _objectsToAdd.Clear();
            _objectsToRemove.Clear();
        }
    }
}