namespace Game.Blocks;

public struct MovingBlocksRaycastResult
{
    public Ray3 Ray;

    public IMovingBlockSet MovingBlockSet;

    public float Distance;

    public Vector3 HitPoint()
    {
        return Ray.Position + Ray.Direction * Distance;
    }
}
