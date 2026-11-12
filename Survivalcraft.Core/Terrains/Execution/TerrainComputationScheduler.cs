using System.Collections.Concurrent;

using Game.Network.Chunks;
using Game.Terrains.Distribution;
using Game.Terrains.Lighting;

namespace Game.Terrains.Execution;

/// <summary>Schedules only runnable terrain stages; prerequisites are resumed by explicit publications.</summary>
public sealed class TerrainComputationScheduler : IDisposable
{
    private readonly TerrainWorkQueue _queue = new();

    private readonly ConcurrentQueue<TerrainWorkKey> _initialization = new();

    private readonly ConcurrentDictionary<TerrainWorkKey, byte> _pendingInitialization = new();

    private TerrainUpdateLocation[] _locations = [];

    private ITerrainDerivedWork? _derived;

    private readonly SubsystemTerrain _world;

    private readonly TerrainLightingCalculator _lighting;

    private readonly TerrainLightWorkerPool _lightWorkers;

    private readonly ConcurrentQueue<TerrainLightCompletion> _lightCompletions = new();

    private readonly object _completionSync = new();

    private readonly Dictionary<TerrainWorkKey, Propagation> _propagations = [];

    private readonly Func<TerrainWorkKey, bool> _canExecute;

    private bool _lightCapacity;

    private volatile bool _disposed;


    private sealed record Propagation(
        TerrainWorkQueue.Item Item,
        TerrainDerivedRevisions.Request Request,
        TerrainLightTask Task,
        TerrainWorkPriority Priority,
        bool AgingSelection
    );

    public TerrainComputationScheduler(
        SubsystemTerrain world,
        TerrainLightingCalculator lighting,
        int lightWorkerCount = 1
    )
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(lighting);
        _world = world;
        _lighting = lighting;
        _lightWorkers = new TerrainLightWorkerPool(
            lightWorkerCount,
            static (job, cancellation) => job.Compute(cancellation),
            OnLightCompleted
        );
        _canExecute = CanExecute;
    }

    internal int ActiveLightJobs => _propagations.Count;

    internal int PendingLightCompletions => _lightCompletions.Count;

    private bool CanExecute(TerrainWorkKey key) => key.Stage != TerrainWorkStage.Propagation || _lightCapacity;

    private void OnLightCompleted(TerrainLightCompletion completion)
    {
        lock (_completionSync)
        {
            if (_disposed) { return; }

            _lightCompletions.Enqueue(completion);
            _world.WorkExecutor.Wake();
        }
    }

    public (int Total, int Ready, int Waiting) Counts => _queue.Counts;


    public bool IsDerivedRequested(TerrainChunk chunk) =>
        !_disposed && !chunk.IsDisposed && _derived != null && IsVisible(chunk);

    /// <summary>Resumes an externally deferred client job only for the same live allocation.</summary>
    public void ResumeDerived(TerrainChunk chunk, ulong allocationGeneration)
    {
        if (_disposed || chunk.IsDisposed || chunk.AllocationGeneration != allocationGeneration) { return; }

        _queue.Signal(new TerrainWorkKey(chunk, TerrainWorkStage.Derived));
        _world.WorkExecutor.Wake();
    }

    public event Action<TerrainChunk>? DerivedInputsInvalidated;

    public void AttachDerivedWork(ITerrainDerivedWork work)
    {
        ArgumentNullException.ThrowIfNull(work);
        _world.WorkExecutor.EnterMain();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_derived != null || _world.ContentRole != TerrainContentRole.Replica)
            {
                throw new InvalidOperationException("Derived work requires an unbound replica terrain scheduler.");
            }

            BindDerived(work);
        }
        finally { _world.WorkExecutor.LeaveMain(); }

        _world.WorkExecutor.Wake();
    }

    public void DetachDerivedWork(ITerrainDerivedWork work)
    {
        ArgumentNullException.ThrowIfNull(work);
        _world.WorkExecutor.EnterMain();
        try
        {
            if (ReferenceEquals(_derived, work)) { BindDerived(null); }
        }
        finally { _world.WorkExecutor.LeaveMain(); }
    }

    private void BindDerived(ITerrainDerivedWork? work)
    {
        _derived = work;
        foreach (var chunk in _world.Terrain.AllocatedChunks) { Request(chunk); }
    }

    public void InvalidateContentNeighborhood(Point2 coordinates, int radius)
    {
        if (_disposed || !WorldRuntimeContext.IsProjectActive(_world.Project)) { return; }

        for (var x = -radius; x <= radius; x++)
        {
            for (var z = -radius; z <= radius; z++)
            {
                var chunk = _world.Terrain.GetChunkAtCoords(coordinates.X + x, coordinates.Y + z);
                if (chunk == null) { continue; }

                if (chunk.Coords == coordinates) { chunk.DerivedRevisions.InvalidateHeightsAndLight(); }
                else { chunk.DerivedRevisions.InvalidatePropagation(); }

                DerivedInputsInvalidated?.Invoke(chunk);
                if (_disposed || !WorldRuntimeContext.IsProjectActive(_world.Project)) { return; }

                Request(chunk);
            }
        }
    }

    public void UpdateDemand(TerrainUpdateLocation[] locations)
    {
        _locations = locations.ToArray();
        foreach (var chunk in _world.Terrain.AllocatedChunks)
        {
            if (!IsNeeded(chunk)) { _queue.Cancel(chunk); }
        }

        foreach (var chunk in _world.Terrain.AllocatedChunks) { Request(chunk); }
    }

    public void ContentAvailable(TerrainChunk chunk)
    {
        chunk.DerivedRevisions.InvalidateHeightsAndLight();
        foreach (var neighbor in Neighbors(chunk))
        {
            if (!ReferenceEquals(neighbor, chunk) && neighbor.HasContents)
            {
                neighbor.DerivedRevisions.InvalidatePropagation();
                Request(neighbor);
            }
        }

        _queue.Signal(new TerrainWorkKey(chunk, TerrainWorkStage.Content));
        Request(chunk);
    }

    public void Retired(TerrainChunk chunk)
    {
        CancelLightJobs(chunk);
        _queue.Cancel(chunk);
        _pendingInitialization.TryRemove(new TerrainWorkKey(chunk, TerrainWorkStage.Content), out _);
    }

    public void Renewed(TerrainChunk chunk, ulong previousGeneration)
    {
        CancelLightJobs(chunk);
        _queue.Cancel(chunk);
        Request(chunk);
    }

    public void Request(TerrainChunk chunk, TerrainWorkPriority? inherited = null)
    {
        if (_disposed || chunk.IsDisposed) { return; }

        if (!IsNeeded(chunk) && !_queue.HasDependents(chunk)) { return; }

        var stage = NextStage(chunk);
        if (stage == null)
        {
            QueueInitialization(chunk);
            return;
        }

        var priority = Priority(chunk, stage.Value);
        var dependentPriority = _queue.GetDependentPriority(chunk);
        if (dependentPriority is { } waiting && waiting < priority) { priority = waiting; }

        if (inherited is { } parent && parent < priority) { priority = parent; }

        var request = chunk.DerivedRevisions.Capture();
        var revision = stage.Value == TerrainWorkStage.Heights ? request.Heights : request.Lighting;
        _queue.Enqueue(new TerrainWorkKey(chunk, stage.Value), revision, priority, Distance(chunk));
        _world.WorkExecutor.Wake();
    }

    public bool Step()
    {
        if (_disposed) { return true; }

        DrainLightCompletions();
        ValidatePropagations();
        _lightCapacity = _lightWorkers.HasCapacity;
        var item = _queue.Take(_canExecute);
        if (item == null) { return true; }

        var started = Stopwatch.GetTimestamp();
        var priority = item.Priority;
        try
        {
            var chunk = item.Key.Chunk;
            if (!IsNeeded(chunk) && !_queue.HasDependents(chunk))
            {
                _queue.Cancel(chunk);
                return false;
            }

            var missing = Dependencies(item.Key).Distinct().ToArray();
            if (missing.Length != 0)
            {
                _queue.Wait(item, missing);
                foreach (var dependency in missing) { Request(dependency.Chunk, item.Priority); }

                return false;
            }

            Execute(item);
            return false;
        }
        catch
        {
            _queue.Cancel(item.Key.Chunk);
            if (_propagations.TryGetValue(item.Key, out var active)) { active.Task.Cancel(); }

            throw;
        }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(started);
            _queue.RecordCost(priority, elapsed);
        }
    }

    public void PrepareHeightsSynchronously(IReadOnlyList<TerrainChunk> chunks, TimeSpan maximumTime)
    {
        foreach (var chunk in chunks)
        {
            if (!chunk.HasCurrentHeightData) { Request(chunk, TerrainWorkPriority.Critical); }
        }

        var started = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(started) < maximumTime &&
               chunks.Any(chunk => chunk.HasContents && !chunk.HasCurrentHeightData))
        {
            if (Step()) { break; }
        }
    }

    private void Execute(TerrainWorkQueue.Item item)
    {
        var chunk = item.Key.Chunk;
        var request = chunk.DerivedRevisions.Capture();
        switch (item.Key.Stage)
        {
            case TerrainWorkStage.Content:
            {
                if (chunk.HasContents && !chunk.NeedsContentValidation)
                {
                    Complete(item, true);
                    break;
                }

                var generation = _world.AuthorityContents?.Generation;
                if (generation == null ||
                    generation.GetStage(chunk) is TerrainGenerationStage.Publication
                        or TerrainGenerationStage.Publishing or TerrainGenerationStage.Rejected)
                {
                    _queue.Wait(item, []);
                    break;
                }

                generation.TryAdvance(chunk);
                if (chunk.HasContents)
                {
                    ContentAvailable(chunk);
                    Complete(item, true);
                }
                else if (generation.GetStage(chunk) == TerrainGenerationStage.Publication) { _queue.Wait(item, []); }
                else { _queue.Finish(item, false); }

                break;
            }
            case TerrainWorkStage.Heights:
                if (chunk.DerivedRevisions.HasCurrentHeights)
                {
                    Complete(item, true);
                    break;
                }

                if (!chunk.DerivedRevisions.HasCurrentHeights)
                {
                    _lighting.CalculateHeights(chunk);
                }

                Complete(item, chunk.DerivedRevisions.PublishHeights(request));
                break;
            case TerrainWorkStage.LightSeed:
                if (chunk.DerivedRevisions.HasCurrentLightSeed && _world.Terrain.Lighting.TryGetSeed(chunk, out _))
                {
                    Complete(item, true);
                    break;
                }

                var seed = _lighting.CalculateLightSeed(chunk);
                var seedReady = seed.Published && chunk.DerivedRevisions.PublishLightSeed(request);
                if (seedReady && seed.ChangedBoundaryFaces != 0) { RecordChanges(chunk, seed.ChangedBoundaryFaces); }

                Complete(item, seedReady);
                break;
            case TerrainWorkStage.Propagation:
                AdvancePropagation(item);
                break;
            case TerrainWorkStage.Derived:
                if (_derived == null || _derived.HasCurrentGeometry(chunk)) { Complete(item, true); }
                else
                {
                    switch (_derived.UpdateChunk(chunk))
                    {
                        case TerrainDerivedWorkResult.Complete:
                            Complete(item, true);
                            break;
                        case TerrainDerivedWorkResult.Continue:
                            Complete(item, false);
                            break;
                        case TerrainDerivedWorkResult.Waiting:
                            _queue.Wait(item, []);
                            break;
                        default:
                            throw new InvalidOperationException("Unknown terrain derived work result.");
                    }
                }

                break;
        }
    }

    private void AdvancePropagation(TerrainWorkQueue.Item item)
    {
        var chunk = item.Key.Chunk;
        if (chunk.DerivedRevisions.HasCurrentLight)
        {
            Complete(item, true);
            return;
        }

        if (_propagations.ContainsKey(item.Key))
        {
            _queue.Wait(item, []);
            return;
        }

        if (!_lighting.TryCapturePropagation(chunk, out var job) || !_lightWorkers.TrySchedule(job, out var task))
        {
            _queue.Finish(item, false);
            return;
        }

        _propagations.Add(
            item.Key,
            new Propagation(item, job.Input.Request, task, item.Priority, item.WasAgingSelection)
        );
        // Completion is a durable message. It can arrive before this waiter is registered.
        _queue.Wait(item, []);
    }

    private void DrainLightCompletions()
    {
        while (_lightCompletions.TryDequeue(out var completion))
        {
            var key = new TerrainWorkKey(
                completion.Task.Job.Chunk,
                completion.Task.Job.Input.Allocation,
                TerrainWorkStage.Propagation
            );
            if (!_propagations.TryGetValue(key, out var active) || !ReferenceEquals(active.Task, completion.Task))
            {
                continue;
            }

            _propagations.Remove(key);
            _queue.RecordCost(active.Priority, completion.Elapsed, active.AgingSelection);
            if (!key.IsCurrent || !_queue.IsRegistered(active.Item)) { continue; }

            if (!_world.IsTerrainActive || (!IsNeeded(key.Chunk) && !_queue.HasDependents(key.Chunk)))
            {
                _queue.Cancel(key.Chunk);
                continue;
            }

            if (completion.Error != null)
            {
                _queue.Cancel(key.Chunk);
                Log.Error($"Terrain lighting calculation failed. {completion.Error}");
                continue;
            }

            var publication = completion.Snapshot != null && !completion.Task.CancellationRequested
                ? _lighting.Publish(completion.Task.Job, completion.Snapshot)
                : default;
            var completed = publication.Published &&
                            key.Chunk.DerivedRevisions.PublishLight(active.Request, publication.ValuesChanged);
            _queue.Finish(active.Item, completed);
            if (completed && (publication.ValuesChanged || publication.ChangedBoundaryFaces != 0))
            {
                RecordChanges(key.Chunk, publication.ChangedBoundaryFaces);
            }

            Request(key.Chunk);
        }
    }

    private void Complete(TerrainWorkQueue.Item item, bool complete)
    {
        _queue.Finish(item, complete);
        if (complete) { Request(item.Key.Chunk); }
    }

    private IEnumerable<TerrainWorkKey> Dependencies(TerrainWorkKey key)
    {
        var chunk = key.Chunk;
        if (key.Stage == TerrainWorkStage.Content) { yield break; }

        if (!chunk.HasContents || chunk.NeedsContentValidation)
        {
            yield return new TerrainWorkKey(chunk, TerrainWorkStage.Content);
            yield break;
        }

        if (key.Stage == TerrainWorkStage.Heights) { yield break; }

        if (!chunk.DerivedRevisions.HasCurrentHeights)
        {
            yield return new TerrainWorkKey(chunk, TerrainWorkStage.Heights);
            yield break;
        }

        if (key.Stage == TerrainWorkStage.LightSeed) { yield break; }

        if (!chunk.DerivedRevisions.HasCurrentLightSeed)
        {
            yield return new TerrainWorkKey(chunk, TerrainWorkStage.LightSeed);
        }

        if (key.Stage == TerrainWorkStage.Propagation) { yield break; }

        foreach (var neighbor in Neighbors(chunk).Where(n => n.HasContents))
        {
            if (neighbor.NeedsContentValidation)
            {
                yield return new TerrainWorkKey(neighbor, TerrainWorkStage.Content);
            }
            else if (!neighbor.DerivedRevisions.HasCurrentLight)
            {
                yield return new TerrainWorkKey(neighbor, TerrainWorkStage.Propagation);
            }
        }
    }

    private TerrainWorkStage? NextStage(TerrainChunk chunk)
    {
        if (!chunk.HasContents || chunk.NeedsContentValidation) { return TerrainWorkStage.Content; }

        if (!chunk.DerivedRevisions.HasCurrentHeights) { return TerrainWorkStage.Heights; }

        if (!chunk.DerivedRevisions.HasCurrentLightSeed || !_world.Terrain.Lighting.TryGetSeed(chunk, out _))
        {
            return TerrainWorkStage.LightSeed;
        }

        if (!chunk.DerivedRevisions.HasCurrentLight) { return TerrainWorkStage.Propagation; }

        return _derived != null && IsVisible(chunk) && !_derived.HasCurrentGeometry(chunk)
            ? TerrainWorkStage.Derived
            : null;
    }

    private void CancelLightJobs(TerrainChunk chunk)
    {
        foreach (var (key, active) in _propagations)
        {
            if (ReferenceEquals(key.Chunk, chunk)) { active.Task.Cancel(); }
        }
    }

    private void ValidatePropagations()
    {
        foreach (var (key, active) in _propagations)
        {
            if (!key.IsCurrent ||
                (!IsNeeded(key.Chunk) && !_queue.HasDependents(key.Chunk)) ||
                key.Chunk.DerivedRevisions.Capture() != active.Request)
            {
                active.Task.Cancel();
            }
        }
    }

    private void RecordChanges(TerrainChunk changed, int boundaryFaces)
    {
        for (var face = 0; face < 4; face++)
        {
            if ((boundaryFaces & (1 << face)) == 0) { continue; }

            var x = changed.Coords.X + (face == 1 ? 1 : face == 3 ? -1 : 0);
            var z = changed.Coords.Y + (face == 0 ? 1 : face == 2 ? -1 : 0);
            var neighbor = _world.Terrain.GetChunkAtCoords(x, z);
            if (neighbor is not { HasContents: true }) { continue; }

            neighbor.DerivedRevisions.InvalidatePropagation();
            Request(neighbor);
        }

        foreach (var neighbor in Neighbors(changed)) { Request(neighbor); }
    }

    private void QueueInitialization(TerrainChunk chunk)
    {
        if (chunk.AreBehaviorsNotified || !chunk.HasCurrentLight) { return; }

        var key = new TerrainWorkKey(chunk, TerrainWorkStage.Content);
        if (_pendingInitialization.TryAdd(key, 0))
        {
            _initialization.Enqueue(key);
        }
    }

    public bool TryTakeInitialization(out TerrainChunk? chunk)
    {
        while (_initialization.TryDequeue(out var key))
        {
            _pendingInitialization.TryRemove(key, out _);
            if (!key.IsCurrent || !key.Chunk.HasCurrentLight || key.Chunk.AreBehaviorsNotified) { continue; }

            chunk = key.Chunk;
            return true;
        }

        chunk = null;
        return false;
    }

    private IEnumerable<TerrainChunk> Neighbors(TerrainChunk chunk)
    {
        for (var x = -1; x <= 1; x++)
        {
            for (var z = -1; z <= 1; z++)
            {
                if (_world.Terrain.GetChunkAtCoords(chunk.Coords.X + x, chunk.Coords.Y + z) is { } neighbor)
                {
                    yield return neighbor;
                }
            }
        }
    }

    private bool IsNeeded(TerrainChunk chunk) => TerrainResidencyManager.IsChunkInRange(chunk.Center, _locations);

    private bool IsVisible(TerrainChunk chunk) => _locations.Any(l =>
        NetworkTerrainPolicy.IsTerrainChunkRelevant(chunk.Center, l.Center, l.VisibilityDistance)
    );

    private float Distance(TerrainChunk chunk) => _locations.Length == 0
        ? float.MaxValue
        : _locations.Min(l => Vector2.DistanceSquared(l.Center, chunk.Center));

    private TerrainWorkPriority Priority(TerrainChunk chunk, TerrainWorkStage stage)
    {
        if (stage == TerrainWorkStage.Content && Distance(chunk) <= 64 * 64) { return TerrainWorkPriority.Critical; }

        if (_derived != null)
        {
            return _derived.HasUploadedGeometry(chunk)
                ? TerrainWorkPriority.Maintenance
                : TerrainWorkPriority.FirstDisplay;
        }

        // Authority initialization includes derived inputs needed before the first baseline.
        return !chunk.IsContentInitialized ? TerrainWorkPriority.FirstDisplay : TerrainWorkPriority.Maintenance;
    }

    public void Dispose()
    {
        lock (_completionSync) { _disposed = true; }

        _lightWorkers.Dispose();
        _propagations.Clear();
        _lightCompletions.Clear();

        _queue.Clear();
        _pendingInitialization.Clear();
        _initialization.Clear();
    }
}
