namespace Game.Terrains.Lighting;

/// <summary>按序保留权威数据，直到匹配的客户端区块内容和高度就绪。</summary>
public sealed class TerrainReplicaLighting(
    Terrain terrain,
    Func<ChunkAllocationId, bool> isCurrent,
    TimeProvider? timeProvider = null
)
{
    private static readonly TimeSpan _appearancePairingBudget = TimeSpan.FromMilliseconds(250);

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

        internal long? waitingForAppearanceSince;

        internal bool queued;
    }

    private readonly Lock _gate = new();

    private readonly Dictionary<Point2, Entry> _entries = [];

    private readonly Queue<ChunkAllocationId> _pending = [];

    internal bool HasReceivedLight(TerrainChunk chunk)
    {
        lock (_gate)
        {
            return _entries.TryGetValue(chunk.Coords, out var entry) &&
                   entry.allocation.Generation == chunk.AllocationGeneration &&
                   entry.contentVersion == chunk.NetworkContentVersion;
        }
    }

    /// <summary>纯灯光外观最多应用到已收到的光照版本。光照缺失时限时等待，避免阻塞后续更新。</summary>
    internal long? GetAppearanceLimit(TerrainChunk chunk)
    {
        lock (_gate)
        {
            var installed = chunk.NetworkContentVersion;
            if (!_entries.TryGetValue(chunk.Coords, out var entry) ||
                entry.allocation.Generation != chunk.AllocationGeneration)
            {
                return null;
            }

            if (IsWaitingForPresentation(entry, CurrentWindow())) { return installed; }

            if (entry.contentVersion > installed)
            {
                entry.waitingForAppearanceSince = null;
                return entry.contentVersion;
            }

            entry.waitingForAppearanceSince ??= _time.GetTimestamp();
            if (_time.GetElapsedTime(entry.waitingForAppearanceSince.Value) < _appearancePairingBudget)
            {
                return installed;
            }

            entry.waitingForAppearanceSince = null;
            return null;
        }
    }

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

        var held = CurrentWindow()?.Groups;
        foreach (var batch in previous)
        {
            if (held?.ContainsKey(batch) != true) { batch.Release(); }
        }

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

    /// <summary>优先呈现已锁定的光照，暂缓与它重叠的后续版本。</summary>
    public void SelectPresentationBatches(HashSet<TerrainLightPresentationBatch> batches)
    {
        lock (_gate)
        {
            if (CurrentWindow() is not { } window) { return; }

            batches.RemoveWhere(batch => !window.Groups.ContainsKey(batch) && OverlapsHeldBatch(batch, window));
            batches.UnionWith(window.Groups.Keys);
        }
    }

    private static bool OverlapsHeldBatch(TerrainLightPresentationBatch batch, PresentationWindow window) =>
        batch.Allocations.Any(window.Allocations.Contains);

    private static bool IsWaitingForPresentation(Entry entry, PresentationWindow? window) =>
        window != null &&
        (window.Allocations.Contains(entry.allocation) ||
         entry.batch is { IsPending: true } batch && OverlapsHeldBatch(batch, window));

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

            if (CurrentWindow() is { } window)
            {
                if (window.Groups.TryGetValue(batch, out var pinned)) { return sources.SequenceEqual(pinned); }

                if (OverlapsHeldBatch(batch, window)) { return false; }
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
                !groups.All(pair => pair.Value.Length != 0 && CanPresent(pair.Key, pair.Value)) ||
                !groups.Values.Any(sources =>
                    sources.Any(source => _entries[source.Allocation.Coords].heldSequence < source.Sequence)
                )) { return false; }

            // Reopening requires new light data. Unchanged neighbors can participate without a new sequence.
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

            if (_window?.Groups.Keys.Any(groups.ContainsKey) == true) { EndWindow(); }
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
            if (HasHeldAppearance(chunk)) { EndWindow(); }
        }
    }

    private PresentationWindow? CurrentWindow()
    {
        if (_window is { } window &&
            (_time.GetElapsedTime(window.Started) >= _presentationBudget ||
             window.Groups.Keys.Any(batch => !batch.IsPending)))
        {
            EndWindow();
        }

        return _window;
    }

    private void EndWindow()
    {
        if (_window is not { } window) { return; }

        _window = null;
        foreach (var batch in window.Groups.Keys)
        {
            if (!batch.Allocations.Any(allocation => ReferenceEquals(FindPendingBatch(allocation), batch)))
            {
                batch.Release();
            }
        }
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

    /// <summary>地形工作线程在持有地形访问锁时调用；数据包回调无需等待此操作。</summary>
    public IReadOnlyList<TerrainChunk> Drain()
    {
        var applied = new List<TerrainChunk>();
        lock (_gate)
        {
            var window = CurrentWindow();
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

                if (IsWaitingForPresentation(entry, window))
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
                    chunk.lightRequestTime = null;
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
                if (_window?.Allocations.Contains(allocation) == true) { EndWindow(); }

                BindPresentation(entry, null);
                _entries.Remove(allocation.Coords);
            }
        }
    }
}
