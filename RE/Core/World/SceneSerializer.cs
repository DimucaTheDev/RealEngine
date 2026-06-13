using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using OpenTK.Mathematics;

namespace RE.Core.World;

public static class SceneSerializer
{
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
}