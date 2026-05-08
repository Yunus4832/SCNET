namespace Game;

public class PathfindingResult
{
    public volatile bool IsCompleted;

    public bool IsInProgress;

    public DynamicArray<Vector3> Path = [];

    public float PathCost;

    public int PositionsChecked;
}
