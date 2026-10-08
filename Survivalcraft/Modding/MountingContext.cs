namespace Game.Modding;

public sealed class MountingContext(ComponentRider rider, ComponentMount mount)
{
    public ComponentRider Rider { get; } = rider;
    public ComponentMount Mount { get; } = mount;
    public bool Cancel { get; set; }
}
