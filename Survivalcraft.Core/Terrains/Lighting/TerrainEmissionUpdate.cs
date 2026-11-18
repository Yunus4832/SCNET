namespace Game.Terrains.Lighting;

/// <summary>Solves an emission edit against current light, with all decreases applied before refill.</summary>
internal static class TerrainEmissionUpdate
{
    private static readonly Point3[] _directions = Enumerable.Range(0, 6).Select(CellFace.FaceToPoint3).ToArray();

    // Point3's default sum hashes entire diagonals into one bucket in a light flood.
    private static readonly EqualityComparer<Point3> _cellComparer = EqualityComparer<Point3>.Create(
        static (first, second) => first == second,
        static cell => HashCode.Combine(cell.X, cell.Y, cell.Z)
    );

    internal static Dictionary<Point3, int> Solve(
        Point3 source,
        Func<Point3, int> published,
        Func<Point3, int> emission,
        Func<Point3, int> attenuation
    )
    {
        var changes = new Dictionary<Point3, int>(_cellComparer);
        var decrease = new Queue<(Point3 Cell, int Level)>();
        var increase = new Queue<(Point3 Cell, int Level)>();
        var old = published(source);
        var next = emission(source);
        changes[source] = next;
        if (old > next) { decrease.Enqueue((source, old)); }

        if (next > 0) { increase.Enqueue((source, next)); }

        foreach (var direction in _directions)
        {
            var neighbor = source + direction;
            var level = published(neighbor);
            if (level > 0) { increase.Enqueue((neighbor, level)); }
        }

        while (decrease.TryDequeue(out var item))
        {
            foreach (var direction in _directions)
            {
                var cell = item.Cell + direction;
                var level = Current(cell);
                if (level <= 0) { continue; }

                if (level < item.Level)
                {
                    var direct = emission(cell);
                    if (direct < level)
                    {
                        changes[cell] = direct;
                        decrease.Enqueue((cell, level));
                    }

                    if (direct > 0) { increase.Enqueue((cell, direct)); }
                }
                else { increase.Enqueue((cell, level)); }
            }
        }

        while (increase.TryDequeue(out var item))
        {
            if (item.Level <= 1 || Current(item.Cell) != item.Level) { continue; }

            foreach (var direction in _directions)
            {
                var cell = item.Cell + direction;
                var current = Current(cell);
                if (item.Level - 1 <= current) { continue; }

                var level = item.Level - attenuation(cell);
                if (level <= current) { continue; }

                changes[cell] = level;
                increase.Enqueue((cell, level));
            }
        }

        return changes;

        int Current(Point3 cell) => changes.TryGetValue(cell, out var value) ? value : published(cell);
    }
}
