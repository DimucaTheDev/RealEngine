namespace RE.Core.Initializing
{
    public class InitializingTask
    {
        internal InitializingTask(){}

        public required Action Action { get; init; }
        public string? Label { get; init; }
    }
}
