using OpenTK.Mathematics;
using System.Reflection;
using System.Text.Json;

namespace RE.Core.World
{
    internal static class SceneManager
    {
        public static Scene CurrentScene { get; private set; }


        public static void LoadScene(Scene scene)
        {
            Initializer.AddStep(($"Loading level \"{scene.Name ?? "<unnamed>"}\"", () =>
            {
                if (CurrentScene != null)
                {
                    CurrentScene.Dispose();
                }
                CurrentScene = scene;
                CurrentScene.Load();
            }
            ));
        }

        public static void LoadScene(string name)
        {
            var assemblyTypes = Assembly.GetExecutingAssembly().GetTypes();
            Scene scene = new Scene();
            scene.Name = name; //TODO: get name from manifest

            string dataPath = Path.Combine("Assets", "Maps", name, $"data.json");
            JsonDocument doc = JsonDocument.Parse(File.ReadAllText(dataPath));
            foreach (var obj in doc.RootElement.EnumerateArray())
            {
                GameObject gameObject = new GameObject();

                gameObject.Name =
                    obj.TryGetProperty("name", out JsonElement nameProperty)
                    ? nameProperty.GetString()!
                    : Random.Shared.Next().ToString(); //TODO


                if (obj.TryGetProperty("transform", out var transformElement))
                {

                    if (transformElement.TryGetProperty("position", out var positionElement))
                    {
                        var array = positionElement.EnumerateArray().Select(s => s.GetSingle()).ToList();
                        gameObject.Transform.Position = new Vector3(array[0], array[1], array[2]);
                    }

                    if (transformElement.TryGetProperty("rotation", out var rotationElement))
                    {
                        var array = rotationElement.EnumerateArray().Select(s => s.GetSingle()).ToList();
                        gameObject.Transform.Rotation = new Quaternion(array[0], array[1], array[2]);
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
                            .First(s => s.Name.ToLower().Replace("component", "") == component.Name.ToLower().Replace("component", ""));

                        object instance;

                        if (component.Value.TryGetProperty("args", out var argsElement) && argsElement.ValueKind == JsonValueKind.Array)
                        {
                            var ctors = type.GetConstructors();
                            ConstructorInfo? matchingCtor = ctors.FirstOrDefault(c => c.GetParameters().Length == argsElement.GetArrayLength());

                            if (matchingCtor == null)
                                throw new InvalidOperationException($"No constructor with {argsElement.GetArrayLength()} arguments found for {type.Name}");

                            var paramInfos = matchingCtor.GetParameters();
                            var parsedArgs = new object?[paramInfos.Length];

                            for (int i = 0; i < paramInfos.Length; i++)
                            {
                                var paramType = paramInfos[i].ParameterType;
                                var argElement = argsElement[i];

                                parsedArgs[i] = JsonSerializer.Deserialize(argElement.GetRawText(), paramType)
                                                ?? throw new InvalidOperationException($"Failed to deserialize argument {i} to {paramType}");
                            }
                            instance = Activator.CreateInstance(type, parsedArgs)!;
                        }
                        else instance = Activator.CreateInstance(type)!;

                        Component c = (Component)instance;
                        gameObject.Components.Add(c);

                        foreach (var prop in component.Value.EnumerateObject())
                        {
                            if (prop.Name.ToLower() == "args") continue;

                            var propertyName = prop.Name;
                            var propertyInfo = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                            var propertyValue = prop.Value.Deserialize(propertyInfo!.PropertyType);

                            if (propertyInfo != null! && propertyInfo.CanWrite)
                            {
                                propertyInfo.SetValue(instance, propertyValue);
                            }
                        }
                    }
                }

                scene.GameObjects.Add(gameObject);
            }

            LoadScene(scene);
        }
    }
}
