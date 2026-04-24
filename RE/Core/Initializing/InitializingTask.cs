namespace RE.Core.Initializing
{
    public sealed class InitializingTask
    {
        public enum TaskType
        {
            Synchronized,
            Asynchronized
        }

        public required Action Action { get; init; }
        public string? Label { get; init; }
        public TaskType Type { get; init; } = TaskType.Synchronized;
    }
}
