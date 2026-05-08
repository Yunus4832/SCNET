namespace EntitySystem.Core;

public class EntityAddRemoveEventArgs(Entity entity) : EventArgs
{
    public Entity Entity { get; } = entity;
}
