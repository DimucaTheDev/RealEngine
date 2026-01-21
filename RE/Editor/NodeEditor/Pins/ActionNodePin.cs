namespace RE.Editor.NodeEditor.Pins
{
    internal class ActionNodePin(string name) : NodePin(name, typeof(void))
    {
        public override object? BoxedValue
        {
            get => null!;
            set { }
        }
          
        public override void Reset() { }
        public override void TakeValueFrom(NodePin link) { }
    }
}