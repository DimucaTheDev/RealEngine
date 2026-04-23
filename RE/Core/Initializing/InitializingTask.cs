namespace RE.Core.Initializing
{
    public sealed class InitializingTask(string label, InitializingTask.TaskType type, Action action)
    {
        public enum TaskType
        {
            Synchronized,
            Asynchronized
        }

        public required Action Action { get; init; } = action;
        public string? Label { get; init; } = label;
        public required TaskType Type { get; init; } = type;
    }
}
