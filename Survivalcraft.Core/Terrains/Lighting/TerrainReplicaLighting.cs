namespace Game.Terrains.Lighting;

/// <summary>Retains ordered authority data until the matching client contents and heights are available.</summary>
public sealed class TerrainReplicaLighting(
    Terrain terrain,
    Func<ChunkAllocationId, bool> isCurrent,
    TimeProvider? timeProvider = null
)
{
    private static readonly TimeSpan _presentationBudget = TimeSpan.FromMilliseconds(500);

    private sealed record PresentationWindow(
        long Started,
        Dictionary<TerrainLightPresentationBatch, TerrainLightPresentationBatch.Source[]> Groups,
        HashSet<ChunkAllocationId> Allocations
    );

    private PresentationWindow? _window;

    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    private sealed class Entry(ChunkAllocationId allocation)
    {
        internal readonly ChunkAllocationId allocation = allocation;

        internal TerrainLightSnapshot? snapshot;

        internal long sequence;

        internal long appliedSequence;

        internal long heldSequence;

        internal TerrainLightPresentationBatch? batch;

        internal ushort pendingSections;

        internal TerrainLightBoundaryChanges pendingBoundaries;

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

        var previous = entry.snapshot;
        entry.snapshot = TerrainLightSectionsCodec.Apply(package, previous);
        entry.sequence = package.Sequence;
        entry.contentVersion = package.ContentVersion;
        _entries[coords] = entry;
        BindPresentation(entry, batch);
        if (batch is { IsPending: true })
        {
            entry.pendingSections |= package.SectionMask;
            entry.pendingBoundaries.Include(previous, entry.snapshot, package.SectionMask);
        }

        if (!entry.queued)
        {
            entry.queued = true;
            _pending.Enqueue(package.Allocation);
        }
    }

    private void BindPresentation(Entry entry, TerrainLightPresentationBatch? batch)
    {
        if (batch == null || entry.batch is not { IsPending: true })
        {
            entry.pendingSections = 0;
            entry.pendingBoundaries = default;
        }

        entry.batch = batch;
        var allocation = entry.allocation;
        var chunk = terrain.GetChunkAtCoords(allocation.Coords.X, allocation.Coords.Y);
        if (chunk?.AllocationGeneration == allocation.Generation) { chunk.LightingPresentationBatch = batch; }
    }

    public TerrainLightPresentationBatch.Source[] CapturePresentationSources(TerrainLightPresentationBatch batch)
    {
        lock (_gate)
        {
            if (CurrentWindow()?.Groups.TryGetValue(batch, out var pinned) == true) { return pinned; }

            var sources = new List<TerrainLightPresentationBatch.Source>();
            foreach (var allocation in batch.Allocations)
            {
                if (ReferenceEquals(FindPendingBatch(allocation), batch))
                {
                    var entry = _entries[allocation.Coords];
                    sources.Add(
                        new TerrainLightPresentationBatch.Source(
                            allocation,
                            entry.sequence,
                            entry.pendingSections,
                            entry.pendingBoundaries
                        )
                    );
                }
            }

            return sources.ToArray();
        }
    }

    public bool CanPresent(
        TerrainLightPresentationBatch batch,
        IReadOnlyList<TerrainLightPresentationBatch.Source> sources
    )
    {
        lock (_gate)
        {
            if (!batch.IsPending) { return false; }

            if (CurrentWindow()?.Groups.TryGetValue(batch, out var pinned) == true)
            {
                return sources.SequenceEqual(pinned);
            }

            foreach (var source in sources)
            {
                if (!ReferenceEquals(FindPendingBatch(source.Allocation), batch)) { return false; }

                var entry = _entries[source.Allocation.Coords];
                if (entry.sequence != source.Sequence ||
                    entry.appliedSequence != source.Sequence ||
                    entry.pendingSections != source.Sections)
                {
                    return false;
                }
            }

            return true;
        }
    }

    public bool TryHoldPresentation(
        IReadOnlyDictionary<TerrainLightPresentationBatch, TerrainLightPresentationBatch.Source[]> groups
    )
    {
        lock (_gate)
        {
            if (CurrentWindow() != null ||
                groups.Count == 0 ||
                !groups.All(pair =>
                    pair.Value.Length != 0 &&
                    CanPresent(pair.Key, pair.Value) &&
                    pair.Value.All(source => _entries[source.Allocation.Coords].heldSequence < source.Sequence)
                )) { return false; }

            // A cancelled or expired window must give the content pump a chance to apply the buffered edits.
            foreach (var sources in groups.Values)
            {
                foreach (var source in sources) { _entries[source.Allocation.Coords].heldSequence = source.Sequence; }
            }

            _window = new PresentationWindow(
                _time.GetTimestamp(),
                new Dictionary<TerrainLightPresentationBatch, TerrainLightPresentationBatch.Source[]>(groups),
                groups.Values.SelectMany(sources => sources.Select(source => source.Allocation)).ToHashSet()
            );
            return true;
        }
    }

    public void CompletePresentation(
        IReadOnlyDictionary<TerrainLightPresentationBatch, TerrainLightPresentationBatch.Source[]> groups
    )
    {
        lock (_gate)
        {
            foreach (var (batch, sources) in groups)
            {
                var newer = sources.Any(source =>
                    _entries.TryGetValue(source.Allocation.Coords, out var entry) &&
                    entry.allocation == source.Allocation &&
                    entry.sequence != source.Sequence
                );
                if (!newer) { batch.Release(); }
            }

            if (_window?.Groups.Keys.Any(groups.ContainsKey) == true) { _window = null; }
        }
    }

    public bool HasHeldAppearance(TerrainChunk chunk)
    {
        lock (_gate)
        {
            return CurrentWindow()?.Allocations
                       .Contains(new ChunkAllocationId(chunk.Coords, chunk.AllocationGeneration)) ==
                   true;
        }
    }

    public void CancelHeldAppearance(TerrainChunk chunk)
    {
        lock (_gate)
        {
            if (HasHeldAppearance(chunk)) { _window = null; }
        }
    }

    private PresentationWindow? CurrentWindow()
    {
        if (_window is { } window &&
            (_time.GetElapsedTime(window.Started) >= _presentationBudget ||
             window.Groups.Keys.Any(batch => !batch.IsPending)))
        {
            _window = null;
        }

        return _window;
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
            var held = CurrentWindow()?.Allocations;
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

                if (held?.Contains(allocation) == true)
                {
                    entry.queued = true;
                    _pending.Enqueue(allocation);
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
                if (_window?.Allocations.Contains(allocation) == true) { _window = null; }

                BindPresentation(entry, null);
                _entries.Remove(allocation.Coords);
            }
        }
    }
}
