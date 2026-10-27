namespace Game.Network.Snapshots;

public static class ExplosionPackageBuilder
{
    public static ExplosionsPackage Capture(IReadOnlyDictionary<Point2, List<(Point3, float)>> cells) =>
        new ExplosionCellsPackage
        {
            Cells = cells.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<ExplosionCell>)pair.Value
                    .Select(value => new ExplosionCell(value.Item1, value.Item2)).ToArray()
            )
        };
}
