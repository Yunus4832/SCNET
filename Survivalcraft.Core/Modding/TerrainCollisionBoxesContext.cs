using EntitySystem.Core;

namespace Game.Modding;

public sealed class TerrainCollisionBoxesContext(
    ComponentBody body,
    BoundingBox bounds,
    DynamicArray<ComponentBody.CollisionBox> collisionBoxes
) : IWorldHookContext
{
    public Project Project => Body.Project;

    public ComponentBody Body { get; } = body;

    public BoundingBox Bounds { get; } = bounds;

    public DynamicArray<ComponentBody.CollisionBox> CollisionBoxes { get; } = collisionBoxes;
}
