using EntitySystem.Core;

namespace Game.Modding;

public sealed class MovingBlockSetTerrainCollisionContext(
    SubsystemMovingBlocks movingBlocks,
    IMovingBlockSet blockSet,
    Point3 min,
    Point3 max
) : IWorldHookContext
{
    public Project Project => MovingBlocks.Project;

    public SubsystemMovingBlocks MovingBlocks { get; } = movingBlocks;

    public IMovingBlockSet BlockSet { get; } = blockSet;

    public Point3 Min { get; } = min;

    public Point3 Max { get; } = max;
}
