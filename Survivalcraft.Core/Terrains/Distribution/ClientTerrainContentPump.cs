using Game.Terrains.Execution;

namespace Game.Terrains.Distribution;

/// <summary>Owns client content delivery handoff, application, and demand-driven retry scheduling.</summary>
public sealed class ClientTerrainContentPump : IDisposable
{
    public const double NetworkChunkRequestRetrySeconds = 5.0;

    private readonly SubsystemTerrain _world;

    private readonly IChunkContentTransport _transport;

    private readonly ClientChunkContentApplication _application;

    private readonly TerrainMainWorkRequest _contentRequest;

    public ClientTerrainContentPump(
        SubsystemTerrain world,
        IChunkContentTransport transport,
        ClientChunkContentCoordinator contents,
        ClientTerrainDeltaCoordinator deltas
    )
    {
        _world = world;
        _transport = transport;
        _application = new ClientChunkContentApplication(transport, contents, deltas);
        _contentRequest = world.WorkExecutor.CreateMainRequest();
    }

    public bool TryDrainReceivedContents()
    {
        if (!_transport.HasPendingWork && !_contentRequest.IsPending) { return true; }

        // Keep the foreground request latched across frames until the worker yields.
        // Other foreground requests and mesh wakeups cannot consume this handoff.
        if (!_contentRequest.TryEnter()) { return false; }

        try
        {
            _transport.SetPriorityCenters(
                _world.Demands.Locations.Where(item => item.Key >= 0)
                    .Select(item => item.Value.Center).ToArray()
            );
            _application.Pump();
            return true;
        }
        finally { _contentRequest.Leave(); }
    }

    public void UpdateRequests()
    {
        if (RequestChunkContents(
                _world.Terrain.AllocatedChunks,
                _world.Demands.Locations.Values.ToArray(),
                _transport,
                Time.RealTime
            ) >
            0)
        {
            _world.WorkExecutor.Wake();
        }
    }

    public void OnContentInstalled(TerrainChunk chunk)
    {
        chunk.IsRequested = false;
        chunk.NetworkRequestTime = 0.0;
        _world.WorkExecutor.Wake();
    }

    public void Dispose() => _contentRequest.Dispose();

    internal static int RequestChunkContents(
        IEnumerable<TerrainChunk> chunks,
        TerrainUpdateLocation[] locations,
        IChunkContentTransport transport,
        double now
    )
    {
        if (locations.Length == 0) { return 0; }

        var selected = chunks.Where(chunk => (!chunk.HasContents || chunk.NeedsContentSnapshot) &&
                                             TerrainResidencyManager.IsChunkInRange(chunk.Center, locations) &&
                                             ShouldRequestNetworkChunk(chunk, now)
            )
            .OrderBy(chunk => locations.Min(location => Vector2.DistanceSquared(chunk.Center, location.Center)))
            .ThenBy(chunk => chunk.Coords.X).ThenBy(chunk => chunk.Coords.Y)
            .Take(TerrainChunksRequestPackage.MaximumRequests).ToArray();
        if (selected.Length == 0) { return 0; }

        transport.Request(
            selected.Select(chunk => new ChunkContentRequest(
                    new ChunkAllocationId(chunk.Coords, chunk.AllocationGeneration),
                    chunk.NetworkContentVersion
                )
            ).ToArray()
        );
        foreach (var chunk in selected)
        {
            chunk.IsRequested = true;
            chunk.NetworkRequestTime = now;
        }

        return selected.Length;
    }

    public static bool ShouldRequestNetworkChunk(TerrainChunk chunk, double now)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        return !chunk.IsRequested ||
               now < chunk.NetworkRequestTime ||
               now - chunk.NetworkRequestTime >= NetworkChunkRequestRetrySeconds;
    }
}
