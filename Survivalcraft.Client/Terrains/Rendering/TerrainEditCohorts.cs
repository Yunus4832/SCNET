namespace Game.Terrains.Rendering;

/// <summary>Visible chunk boundaries affected by a local geometry prediction switch together.</summary>
internal sealed class TerrainEditCohorts(Terrain terrain, Action<TerrainChunk, bool> interactive)
{
    private readonly Dictionary<TerrainChunk, HashSet<TerrainChunk>> _membership = [];

    internal IEnumerable<HashSet<TerrainChunk>> Groups => _membership.Values.Distinct();

    internal HashSet<TerrainChunk>? Find(TerrainChunk chunk) => _membership.GetValueOrDefault(chunk);

    internal void Track(Point3 cell)
    {
        var x = cell.X >> 4;
        var z = cell.Z >> 4;
        var dx = (cell.X & 15) == 0 ? -1 : (cell.X & 15) == 15 ? 1 : 0;
        var dz = (cell.Z & 15) == 0 ? -1 : (cell.Z & 15) == 15 ? 1 : 0;
        var chunks = new List<TerrainChunk>();
        // Only face neighbors can expose a missing sealing face. Diagonal lighting changes
        // remain ordinary maintenance and must not hold up the topology switch.
        foreach (var offset in new[] { new Point2(0, 0), new Point2(dx, 0), new Point2(0, dz) }.Distinct())
        {
            if (terrain.GetChunkAtCoords(x + offset.X, z + offset.Y) is { HasContents: true } chunk)
            {
                chunks.Add(chunk);
            }
        }

        if (chunks.Count == 1)
        {
            interactive(chunks[0], true);
            return;
        }

        if (chunks.Count == 0) { return; }

        var group = chunks.Select(Find).FirstOrDefault(existing => existing != null) ?? [];
        foreach (var chunk in chunks)
        {
            if (Find(chunk) is { } existing && !ReferenceEquals(existing, group))
            {
                foreach (var member in existing) { group.Add(member); }
            }

            group.Add(chunk);
        }

        foreach (var chunk in group)
        {
            _membership[chunk] = group;
            interactive(chunk, true);
        }
    }

    internal void Complete(HashSet<TerrainChunk> group)
    {
        foreach (var chunk in group)
        {
            if (!ReferenceEquals(Find(chunk), group)) { continue; }

            _membership.Remove(chunk);
            interactive(chunk, false);
        }
    }

    internal void Release(TerrainChunk chunk)
    {
        if (_membership.Remove(chunk, out var group)) { group.Remove(chunk); }

        interactive(chunk, false);
    }

    internal void Clear()
    {
        foreach (var chunk in _membership.Keys.ToArray()) { interactive(chunk, false); }

        _membership.Clear();
    }
}
