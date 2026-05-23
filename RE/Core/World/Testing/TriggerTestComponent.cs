using RE.Core.Scripting.Attributes;
using RE.Core.World.Components.Physics;

namespace RE.Core.World.Testing;

[RequiresComponent<RigidBodyComponent>]
[RequiresComponent<BoxColliderComponent>]
public class TriggerTestComponent : Component
{
    /// <inheritdoc />
    public override void Start()
    {
        GetComponent<RigidBodyComponent>()!.IsTrigger = true;
    }

    /// <inheritdoc />
    public override void OnCollisionEnter(GameObject collide)
    {
        if (!collide.GetComponent<RigidBodyComponent>()!.IsKinematic)
            collide.SetPosition((0, 5, 0));
    }
}