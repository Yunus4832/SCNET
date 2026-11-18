namespace Game.Terrains.Lighting;

/// <summary>Retains ordered authority data until the matching client contents and heights are available.</summary>
public sealed class TerrainReplicaLighting(Terrain terrain, Func<ChunkAllocationId, bool> isCurrent)
{
    private sealed class Entry(ChunkAllocationId allocation)
    {
        internal readonly ChunkAllocationId allocation = allocation;

        internal TerrainLightSnapshot? snapshot;

        internal long sequence;

        internal long appliedSequence;

        internal TerrainLightPresentationBatch? batch;

        internal long contentVersion;

        internal bool queued;
    }

    private readonly Lock _gate = new();

    private readonly Dictionary<Point2, Entry> _entries = [];

    private readonly Queue<ChunkAllocationId> _pending = [];

    public bool Receive(TerrainLightSectionsPackage package)
    {
        ProtocolMessageValidation.Validate(package);
        lock (_gate)
        {
            if (!CanReceive(package)) { return false; }

            Accept(package, FindPendingBatch(package.Allocation));
            return true;
        }
    }

    public bool Receive(TerrainLightBatchPackage package)
    {
        ProtocolMessageValidation.Validate(package);
        lock (_gate)
        {
            var updates = package.Updates.Where(CanReceive).ToArray();
            var batch = updates.Length > 1 && updates.Any(update => update.SectionMask != 0)
                ? MergePresentation(updates)
                : null;
            foreach (var update in updates)
            {
                Accept(update, batch ?? FindPendingBatch(update.Allocation));
            }

            return updates.Length != 0;
        }
    }

    private TerrainLightPresentationBatch? FindPendingBatch(ChunkAllocationId allocation)
    {
        return _entries.TryGetValue(allocation.Coords, out var entry) &&
               entry.allocation == allocation &&
               entry.batch is { IsPending: true } batch
            ? batch
            : null;
    }

    private TerrainLightPresentationBatch MergePresentation(TerrainLightSectionsPackage[] updates)
    {
        var allocations = updates.Select(update => update.Allocation).ToHashSet();
        var previous = new HashSet<TerrainLightPresentationBatch>();
        foreach (var update in updates)
        {
            if (FindPendingBatch(update.Allocation) is { } batch) { previous.Add(batch); }
        }

        if (previous.Count == 1)
        {
            var current = previous.Single();
            if (allocations.All(allocation => ReferenceEquals(FindPendingBatch(allocation), current)))
            {
                return current;
            }
        }

        foreach (var batch in previous)
        {
            allocations.UnionWith(
                batch.Allocations.Where(allocation => ReferenceEquals(FindPendingBatch(allocation), batch))
            );
        }

        var merged = new TerrainLightPresentationBatch(allocations.ToArray());
        merged.Activate();
        foreach (var allocation in allocations)
        {
            if (_entries.TryGetValue(allocation.Coords, out var entry) && entry.allocation == allocation)
            {
                BindPresentation(entry, merged);
            }
        }

        foreach (var batch in previous) { batch.Release(); }

        return merged;
    }

    private bool CanReceive(TerrainLightSectionsPackage package)
    {
        if (!isCurrent(package.Allocation)) { return false; }

        _entries.TryGetValue(package.Allocation.Coords, out var entry);
        if (entry?.allocation != package.Allocation) { entry = null; }

        if (entry != null && (package.Sequence <= entry.sequence || package.ContentVersion < entry.contentVersion))
        {
            return false;
        }

        return package.IsBaseline || entry != null && entry.sequence == package.BaseSequence;
    }

    private void Accept(TerrainLightSectionsPackage package, TerrainLightPresentationBatch? batch)
    {
        var coords = package.Allocation.Coords;
        _entries.TryGetValue(coords, out var entry);
        if (entry?.allocation != package.Allocation) { entry = new Entry(package.Allocation); }

        entry.snapshot = TerrainLightSectionsCodec.Apply(package, entry.snapshot);
        entry.sequence = package.Sequence;
        entry.contentVersion = package.ContentVersion;
        _entries[coords] = entry;
        BindPresentation(entry, batch);

        if (!entry.queued)
        {
            entry.queued = true;
            _pending.Enqueue(package.Allocation);
        }
    }

    private void BindPresentation(Entry entry, TerrainLightPresentationBatch? batch)
    {
        entry.batch = batch;
        var allocation = entry.allocation;
        var chunk = terrain.GetChunkAtCoords(allocation.Coords.X, allocation.Coords.Y);
        if (chunk?.AllocationGeneration == allocation.Generation) { chunk.LightingPresentationBatch = batch; }
    }

    public bool IsPresentationApplied(TerrainChunk chunk, TerrainLightPresentationBatch batch)
    {
        lock (_gate)
        {
            return _entries.TryGetValue(chunk.Coords, out var entry) &&
                   entry.allocation.Generation == chunk.AllocationGeneration &&
                   ReferenceEquals(entry.batch, batch) &&
                   entry.appliedSequence == entry.sequence;
        }
    }

    /// <summary>Called by the terrain worker under the terrain access gate; packet callbacks never wait for it.</summary>
    public IReadOnlyList<TerrainChunk> Drain()
    {
        var applied = new List<TerrainChunk>();
        lock (_gate)
        {
            var count = Math.Min(32, _pending.Count);
            for (var i = 0; i < count; i++)
            {
                var allocation = _pending.Dequeue();
                if (!_entries.TryGetValue(allocation.Coords, out var entry) || entry.allocation != allocation)
                {
                    continue;
                }

                entry.queued = false;
                if (!isCurrent(allocation))
                {
                    BindPresentation(entry, null);
                    _entries.Remove(allocation.Coords);
                    continue;
                }

                var chunk = terrain.GetChunkAtCoords(allocation.Coords.X, allocation.Coords.Y);
                if (chunk is not { HasContents: true, HasCurrentHeightData: true, NeedsContentValidation: false } ||
                    chunk.NetworkContentVersion != entry.contentVersion)
                {
                    entry.queued = true;
                    _pending.Enqueue(allocation);
                    continue;
                }

                chunk.LightingPresentationBatch = entry.batch;
                var request = chunk.DerivedRevisions.Capture();
                if (!chunk.DerivedRevisions.PublishLightSeed(request)) { continue; }

                terrain.Lighting.TryGetPublishedSnapshot(chunk, out var previous);
                if (terrain.Lighting.TryPublish(chunk, terrain.Lighting.CaptureInput(chunk), entry.snapshot!) &&
                    chunk.DerivedRevisions.PublishLight(request, !ReferenceEquals(previous, entry.snapshot)))
                {
                    entry.appliedSequence = entry.sequence;
                    applied.Add(chunk);
                }
                else
                {
                    entry.queued = true;
                    _pending.Enqueue(allocation);
                }
            }
        }

        return applied;
    }

    public void Requeue(TerrainChunk chunk)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(chunk.Coords, out var entry) && !entry.queued)
            {
                entry.queued = true;
                _pending.Enqueue(entry.allocation);
            }
        }
    }

    public void Discard(ChunkAllocationId allocation)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(allocation.Coords, out var entry) && entry.allocation == allocation)
            {
                BindPresentation(entry, null);
                _entries.Remove(allocation.Coords);
            }
        }
    }
}
