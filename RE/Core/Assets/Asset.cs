namespace RE.Core.Assets
{
    internal abstract class Asset
    {
        public virtual void OnLoad() { }
        public virtual void OnUnload() { }
    }
}
