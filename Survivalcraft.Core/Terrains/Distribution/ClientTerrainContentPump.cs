using Game.Terrains.Execution;
using Game.Terrains.Lighting;

namespace Game.Terrains.Distribution;

/// <summary>管理客户端内容的交接、应用及按需重试调度。</summary>
public sealed class ClientTerrainContentPump(
    SubsystemTerrain world,
    IChunkContentTransport transport,
    ClientChunkContentCoordinator contents,
    ClientTerrainDeltaCoordinator deltas
) : IDisposable
{
    public const double NetworkChunkRequestRetrySeconds = 5.0;

    private readonly ClientChunkContentApplication _application = new(transport, contents, deltas);

    private readonly TerrainMainWorkRequest _contentRequest = world.WorkExecutor.CreateMainRequest();

    public bool TryDrainReceivedContents()
    {
        if (!transport.HasPendingWork && !_application.HasDeferredPresentation && !_contentRequest.IsPending)
        {
            return true;
        }

        // Keep the foreground request latched across frames until the worker yields.
        // Other foreground requests and mesh wakeups cannot consume this handoff.
        if (!_contentRequest.TryEnter()) { return false; }

        try
        {
            transport.SetPriorityCenters(
                world.Demands.Locations.Where(item => item.Key >= 0)
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
                world.Terrain.AllocatedChunks,
                world.Demands.Locations.Values.ToArray(),
                transport,
                Time.RealTime,
                world.ReplicaLighting
            ) >
            0)
        {
            world.WorkExecutor.Wake();
        }
    }

    public void OnContentInstalled(TerrainChunk chunk)
    {
        chunk.IsRequested = false;
        chunk.NetworkRequestTime = 0.0;
        chunk.lightRequestTime = null;
        world.WorkExecutor.Wake();
    }

    public void Dispose() => _contentRequest.Dispose();

    internal static int RequestChunkContents(
        IEnumerable<TerrainChunk> chunks,
        TerrainUpdateLocation[] locations,
        IChunkContentTransport transport,
        double now,
        TerrainReplicaLighting? lighting = null
    )
    {
        if (locations.Length == 0) { return 0; }

        var selected = chunks
            .Select(chunk => (Chunk: chunk, NeedsContent: !chunk.HasContents || chunk.NeedsContentSnapshot))
            .Where(item => (item.NeedsContent || lighting != null && !item.Chunk.HasCurrentLight) &&
                           TerrainResidencyManager.IsChunkInRange(item.Chunk.Center, locations)
            )
            .Where(item => item.NeedsContent
                ? ShouldRequestNetworkChunk(item.Chunk, now)
                : lighting != null && ShouldRequestLightBaseline(item.Chunk, lighting, now)
            )
            .OrderByDescending(item => item.NeedsContent)
            .ThenBy(item => locations.Min(location => Vector2.DistanceSquared(item.Chunk.Center, location.Center)))
            .ThenBy(item => item.Chunk.Coords.X).ThenBy(item => item.Chunk.Coords.Y)
            .Take(TerrainChunksRequestPackage.MaximumRequests).ToArray();
        if (selected.Length == 0) { return 0; }

        transport.Request(
            selected.Select(item => new ChunkContentRequest(
                    new ChunkAllocationId(item.Chunk.Coords, item.Chunk.AllocationGeneration),
                    item.Chunk.NetworkContentVersion,
                    RequestLightBaseline: !item.NeedsContent
                )
            ).ToArray()
        );
        foreach (var (chunk, needsContent) in selected)
        {
            if (needsContent)
            {
                chunk.IsRequested = true;
                chunk.NetworkRequestTime = now;
            }
            else { chunk.lightRequestTime = now; }
        }

        return selected.Length;
    }

    private static bool ShouldRequestLightBaseline(TerrainChunk chunk, TerrainReplicaLighting lighting, double now)
    {
        if (!chunk.HasCurrentHeightData ||
            chunk.HasCurrentLight ||
            chunk.NeedsContentValidation ||
            chunk.NetworkContentVersion <= 0) { return false; }

        if (chunk.lightRequestTime == null || now < chunk.lightRequestTime)
        {
            chunk.lightRequestTime = now;
        }

        return now - chunk.lightRequestTime >= NetworkChunkRequestRetrySeconds && !lighting.HasReceivedLight(chunk);
    }

    public static bool ShouldRequestNetworkChunk(TerrainChunk chunk, double now)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        return !chunk.IsRequested ||
               now < chunk.NetworkRequestTime ||
               now - chunk.NetworkRequestTime >= NetworkChunkRequestRetrySeconds;
    }
}
