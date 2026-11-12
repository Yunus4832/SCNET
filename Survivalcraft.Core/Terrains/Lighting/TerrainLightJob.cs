namespace Game.Terrains.Lighting;

/// <summary>Immutable inputs for a chunk calculation which can execute without world access.</summary>
public sealed class TerrainLightJob
{
    private readonly TerrainLightSeed _seed;

    private readonly TerrainLightSnapshot?[] _neighbors;

    private readonly TerrainLightSnapshot? _previous;

    public TerrainChunk Chunk { get; }

    public TerrainLightInput Input { get; }

    internal TerrainLightJob(
        TerrainChunk chunk,
        TerrainLightInput input,
        TerrainLightSeed seed,
        TerrainLightSnapshot?[] neighbors
    )
    {
        Chunk = chunk;
        Input = input;
        _seed = seed;
        _neighbors = (TerrainLightSnapshot?[])neighbors.Clone();
        chunk.Terrain.Lighting.TryGetPublishedSnapshot(chunk, out _previous);
    }

    internal TerrainLightSnapshot? Neighbor(int face) => _neighbors[face];

    internal TerrainLightPropagation CreateComputation(CancellationToken cancellation = default)
    {
        return new TerrainLightPropagation(_seed, _neighbors, cancellation);
    }

    public TerrainLightSnapshot Compute(CancellationToken cancellation = default)
    {
        using var computation = CreateComputation(cancellation);
        while (!computation.Advance(2048, cancellation)) { }

        cancellation.ThrowIfCancellationRequested();
        return computation.Capture(_previous);
    }
}
