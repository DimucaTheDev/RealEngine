namespace RE.Core.World.Components
{
    internal interface IPhysicsComponent
    {
        bool IsPhysicsObjectInitialized { get; }
        void TryInitializePhysics();
    }

}
