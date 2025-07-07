namespace RE.Core.Scripting
{
    internal class Variables
    {
        public static Dictionary<string, object?> GlobalVariables { get; } = new();

        public static event Action<string, object?> VariableChanged;

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
    }
}
