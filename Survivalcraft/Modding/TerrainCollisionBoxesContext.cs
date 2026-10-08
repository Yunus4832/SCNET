namespace Game.Modding;

public sealed class TerrainCollisionBoxesContext(
    ComponentBody body,
    BoundingBox bounds,
    DynamicArray<ComponentBody.CollisionBox> collisionBoxes)
{
    public ComponentBody Body { get; } = body;
    public BoundingBox Bounds { get; } = bounds;
    public DynamicArray<ComponentBody.CollisionBox> CollisionBoxes { get; } = collisionBoxes;
}
