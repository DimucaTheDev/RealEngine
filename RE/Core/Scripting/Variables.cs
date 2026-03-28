namespace RE.Core.Scripting
{
    /// <summary>
    /// Provides a global key-value store for application-wide variables and notifies subscribers when variables change.
    /// </summary>
    /// <remarks>The Variables class enables storing and retrieving variables that are accessible throughout
    /// the application. It is thread-safe for individual operations on the underlying dictionary, but callers should
    /// ensure appropriate synchronization if performing compound operations. The VariableChanged event is raised
    /// whenever a variable is added or updated using SetVariable.</remarks>
    public class Variables
    {
        public static Dictionary<string, object?> GlobalVariables { get; } = new();

        public static event Action<string, object?>? VariableChanged;

        public static void SetVariable(string key, object value)
        {
            GlobalVariables[key] = value;
            VariableChanged?.Invoke(key, value);
        }

        public static object? GetVariable(string key)
        {
            GlobalVariables.TryGetValue(key, out var value);
            return value ?? null!;
        }

        public static object? GetVariableOrDefault(string key, object? @default)
        {
            GlobalVariables.TryGetValue(key, out var value);
            return value ?? @default;
        }
    }
}
