namespace RE.Core.World.Components.Physics
{
    internal interface IPhysicsComponent
    {
        bool IsPhysicsObjectInitialized { get; }
        void TryInitializePhysics();
    }

}
