using System.Diagnostics.CodeAnalysis;

using Game.Terrains.Lighting;

namespace Game.Terrains.Distribution;

/// <summary>Publishes authority light baselines and changed sections for the existing content allocation lifetimes.</summary>
internal sealed class ServerTerrainLightingReplication : IDisposable
{
    private const int _updatesPerClientTick = 4;

    private readonly Terrain _terrain;

    private readonly IChunkContentAuthority _authority;

    private readonly ServerChunkDistributionScheduler _distribution;

    private readonly IWorldMessageSink _messages;

    // A client's previous sequence can outlive its server-side interest entry.
    private long _nextSequence;

    private bool _disposed;

    private sealed record LightCursor(
        ChunkAllocationId Allocation,
        ulong SourceGeneration,
        long ContentVersion,
        long Revision,
        long Sequence,
        TerrainLightSnapshot Snapshot
    );

    private readonly Dictionary<WorldConnection, Dictionary<Point2, LightCursor>> _lightCursors =
        new(ReferenceEqualityComparer.Instance);

    public ServerTerrainLightingReplication(
        Terrain terrain,
        IChunkContentAuthority authority,
        ServerChunkDistributionScheduler distribution,
        IWorldMessageSink messages
    )
    {
        _terrain = terrain;
        _authority = authority;
        _distribution = distribution;
        _messages = messages;
        _distribution.LightBaselineRequested += OnLightBaselineRequested;
    }

    private void OnLightBaselineRequested(WorldConnection client, ChunkAllocationId allocation)
    {
        if (_lightCursors.TryGetValue(client, out var cursors)) { cursors.Remove(allocation.Coords); }
    }

    public void Update()
    {
        if (_disposed) { return; }

        foreach (var client in _lightCursors.Keys.Where(client => !_distribution.Allocations.ContainsKey(client))
                     .ToArray())
        {
            _lightCursors.Remove(client);
        }

        foreach (var (client, allocations) in _distribution.Allocations)
        {
            if (!client.IsConnected || !_distribution.TryGetClientLocation(client, out var center, out _)) { continue; }

            if (!_lightCursors.TryGetValue(client, out var cursors))
            {
                cursors = [];
                _lightCursors.Add(client, cursors);
            }

            foreach (var coords in cursors.Keys.Where(coords => !allocations.ContainsKey(coords)).ToArray())
            {
                cursors.Remove(coords);
            }

            var sent = 0;
            var visited = new HashSet<TerrainLightingStore.LocalLightBatch>(ReferenceEqualityComparer.Instance);
            foreach (var allocation in allocations.Values.OrderBy(allocation =>
                         Vector2.DistanceSquared(
                             center,
                             new Vector2(allocation.Coords.X * 16 + 8, allocation.Coords.Y * 16 + 8)
                         )
                     ))
            {
                if (sent >= _updatesPerClientTick) { break; }

                var epoch = _terrain.Lighting.RegionalPublicationEpoch;
                if ((epoch & 1) != 0) { break; }

                var chunk = _terrain.GetChunkAtCoords(allocation.Coords.X, allocation.Coords.Y);
                if (chunk == null ||
                    !TryCapture(chunk, allocation, cursors, out var update, out var cursor) ||
                    update == null)
                {
                    continue;
                }

                if (!update.IsBaseline && _terrain.Lighting.GetLocalBatch(chunk) is { } batch)
                {
                    if (!visited.Add(batch) ||
                        !TryCaptureBatch(batch, allocations, cursors, out var updates, out var nextCursors) ||
                        _terrain.Lighting.RegionalPublicationEpoch != epoch)
                    {
                        continue;
                    }

                    if (updates.Length == 0) { continue; }

                    // A whole batch may use the remaining budget and end this client's send pass.
                    var message = updates.Length == 1
                        ? (IProtocolMessage)updates[0]
                        : new TerrainLightBatchPackage { Updates = updates };
                    if (!_messages.TryQueueOwnedTerrainLighting(client, message)) { break; }

                    foreach (var next in nextCursors) { cursors[next.Allocation.Coords] = next; }

                    sent += updates.Length;
                    continue;
                }

                if (_terrain.Lighting.RegionalPublicationEpoch != epoch) { continue; }

                if (!_messages.TryQueueOwnedTerrainLighting(client, update)) { break; }

                cursors[allocation.Coords] = cursor;
                sent++;
            }
        }
    }

    private bool TryCaptureBatch(
        TerrainLightingStore.LocalLightBatch batch,
        Dictionary<Point2, ChunkAllocationId> allocations,
        Dictionary<Point2, LightCursor> cursors,
        out TerrainLightSectionsPackage[] updates,
        out LightCursor[] nextCursors
    )
    {
        updates = [];
        nextCursors = [];
        var pending = new List<TerrainLightSectionsPackage>(batch.Members.Length);
        var next = new List<LightCursor>(batch.Members.Length);
        foreach (var chunk in batch.Members)
        {
            if (chunk.IsDisposed || !ReferenceEquals(_terrain.Lighting.GetLocalBatch(chunk), batch)) { continue; }

            // New subscriptions get their baseline independently of already visible members.
            if (!allocations.TryGetValue(chunk.Coords, out var allocation) ||
                !cursors.TryGetValue(chunk.Coords, out var previous) ||
                previous.Allocation != allocation)
            {
                continue;
            }

            if (!TryCapture(chunk, allocation, cursors, out var update, out var cursor)) { return false; }

            if (update == null) { continue; }

            pending.Add(update);
            next.Add(cursor);
        }

        updates = pending.ToArray();
        nextCursors = next.ToArray();
        return true;
    }

    private bool TryCapture(
        TerrainChunk chunk,
        ChunkAllocationId allocation,
        Dictionary<Point2, LightCursor> cursors,
        out TerrainLightSectionsPackage? update,
        [NotNullWhen(true)] out LightCursor? cursor
    )
    {
        update = null;
        cursor = null;
        if (!_authority.TryGetDescriptor(allocation.Coords, out var descriptor)) { return false; }

        var revision = chunk.DerivedRevisions.LightRevision;
        if (!_terrain.Lighting.TryGetCurrentSnapshot(chunk, out var snapshot) ||
            revision != chunk.DerivedRevisions.LightRevision ||
            chunk.NetworkContentRevision + 1 != descriptor.ContentVersion ||
            !chunk.HasCurrentLight) { return false; }

        cursors.TryGetValue(allocation.Coords, out var previous);
        if (previous?.Allocation != allocation) { previous = null; }

        if (previous != null &&
            previous.Revision == revision &&
            previous.ContentVersion == descriptor.ContentVersion &&
            previous.SourceGeneration == chunk.AllocationGeneration)
        {
            cursor = previous;
            return true;
        }

        var sequence = ++_nextSequence;
        update = TerrainLightSectionsCodec.Capture(
            allocation,
            descriptor.ContentVersion,
            sequence,
            previous?.Sequence ?? 0,
            snapshot,
            previous?.Snapshot
        );
        cursor = new LightCursor(
            allocation,
            chunk.AllocationGeneration,
            descriptor.ContentVersion,
            revision,
            sequence,
            snapshot
        );
        return true;
    }

    public void Dispose()
    {
        _disposed = true;
        _distribution.LightBaselineRequested -= OnLightBaselineRequested;
        _lightCursors.Clear();
    }
}
