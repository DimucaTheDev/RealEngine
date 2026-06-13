using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using OpenTK.Mathematics;
using RE.Core.Assets;
using RE.Core.Initializing;
using RE.Core.PluginSystem;
using RE.Core.Scripting.Attributes;
using RE.Editor.Panels.Viewport;
using RE.Utils;
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
        public static void LoadScene(Scene scene) => LoadScene(scene, true);

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
            SceneChanged = true;

            //Hud.Root.Children.Clear();
            //Log.Debug("Scene changed, HUD canvas cleared");

            if (CurrentScene != null! && disposeCurrent)
            {
                CurrentScene.Dispose();
            }

            CurrentScene = scene;

            if (!CurrentScene.LightSources.Any())
                Log.Warning("No light sources in {SceneName}", CurrentScene.Name);

            afterLoaded?.Invoke();
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
            var jsonString = SceneSerializer.SerializeScene(scene);

            string savedTo;
            if (jsonString.EndsWith(".json")) // a file
            {
                var directoryName = Path.GetDirectoryName(path);
                if (!Directory.Exists(directoryName))
                    Directory.CreateDirectory(directoryName!);
                //todo: deflate compression
                File.WriteAllText(savedTo = path, jsonString);
            }
            else
            {
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                //todo: deflate compression
                File.WriteAllText(savedTo = Path.Combine(path, "data.json"), jsonString);
            }

            Log.Information("Saved level to '{Path}'", savedTo);
        }


        //todo: parse at PATH, not just name
        /// <summary>
        /// Loads and deserializes a scene from JSON file located at <c>Assets/Maps/{name}/data.json</c>.
        /// </summary>
        /// <param name="name">Scene name in <c>Assets/Maps</c></param>
        /// <returns>A new scene instance</returns>
        public static Scene LoadFromMapFile(string name)
        {
            string dataPath = Path.Combine("Assets", "Maps", name, "data.json");

            //todo: deflate decompression
            return SceneSerializer.DeserializeScene(ContentManager.GetString(dataPath), name);
        }


        public static Scene Reload(Scene scene)
        {
            var newScene = SceneSerializer.DeserializeScene(SceneSerializer.SerializeScene(scene), scene.Name);
            return newScene;
        }
    }
}