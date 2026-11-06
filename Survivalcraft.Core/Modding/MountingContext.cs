using EntitySystem.Core;

namespace Game.Modding;

public sealed class MountingContext(ComponentRider rider, ComponentMount mount) : IWorldHookContext
{
    public Project Project => Rider.Project;

    public ComponentRider Rider { get; } = rider;

    public ComponentMount Mount { get; } = mount;

    public bool Cancel { get; set; }
}
