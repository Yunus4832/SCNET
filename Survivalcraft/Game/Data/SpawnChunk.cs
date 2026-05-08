namespace Game;

public class SpawnChunk
{
    public bool IsSpawned;

    public double? LastVisitedTime;

    public Point2 Point;

    public List<SpawnEntityData> SpawnsData = [];
}
