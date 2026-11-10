using System.Collections.Concurrent;

using Game.Network.Chunks;
using Game.Terrains.Distribution;
using Game.Terrains.Lighting;

namespace Game.Terrains.Execution;

/// <summary>Schedules only runnable terrain stages; prerequisites are resumed by explicit publications.</summary>
public sealed class TerrainComputationScheduler(
    SubsystemTerrain world,
    TerrainLightingCalculator lighting
) : IDisposable
{
    private readonly TerrainWorkQueue _queue = new();

    private readonly ConcurrentQueue<TerrainWorkKey> _initialization = new();

    private readonly ConcurrentDictionary<TerrainWorkKey, byte> _pendingInitialization = new();

    private TerrainUpdateLocation[] _locations = [];

    private ITerrainDerivedWork? _derived;

    private Propagation? _propagation;

    private bool _disposed;


    private sealed record Input(
        TerrainChunk Chunk,
        ulong Generation,
        long Contents,
        TerrainDerivedRevisions.Request Request,
        long Heights
    );

    private sealed record Propagation(TerrainWorkKey Key, TerrainDerivedRevisions.Request Request, Input[] Inputs);

    public (int Total, int Ready, int Waiting) Counts => _queue.Counts;


    public bool IsDerivedRequested(TerrainChunk chunk) =>
        !_disposed && !chunk.IsDisposed && _derived != null && IsVisible(chunk);

    /// <summary>Resumes an externally deferred client job only for the same live allocation.</summary>
    public void ResumeDerived(TerrainChunk chunk, ulong allocationGeneration)
    {
        if (_disposed || chunk.IsDisposed || chunk.AllocationGeneration != allocationGeneration) { return; }

        _queue.Signal(new TerrainWorkKey(chunk, TerrainWorkStage.Derived));
        world.WorkExecutor.Wake();
    }

    public event Action<TerrainChunk>? DerivedInputsInvalidated;

    public void AttachDerivedWork(ITerrainDerivedWork work)
    {
        ArgumentNullException.ThrowIfNull(work);
        world.WorkExecutor.EnterMain();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_derived != null || world.ContentRole != TerrainContentRole.Replica)
            {
                throw new InvalidOperationException("Derived work requires an unbound replica terrain scheduler.");
            }

            BindDerived(work);
        }
        finally { world.WorkExecutor.LeaveMain(); }

        world.WorkExecutor.Wake();
    }

    public void DetachDerivedWork(ITerrainDerivedWork work)
    {
        ArgumentNullException.ThrowIfNull(work);
        world.WorkExecutor.EnterMain();
        try
        {
            if (ReferenceEquals(_derived, work)) { BindDerived(null); }
        }
        finally { world.WorkExecutor.LeaveMain(); }
    }

    private void BindDerived(ITerrainDerivedWork? work)
    {
        _derived = work;
        foreach (var chunk in world.Terrain.AllocatedChunks) { Request(chunk); }
    }

    public void InvalidateContentNeighborhood(Point2 coordinates, int radius)
    {
        if (_disposed || !WorldRuntimeContext.IsProjectActive(world.Project)) { return; }

        for (var x = -radius; x <= radius; x++)
        {
            for (var z = -radius; z <= radius; z++)
            {
                var chunk = world.Terrain.GetChunkAtCoords(coordinates.X + x, coordinates.Y + z);
                if (chunk == null) { continue; }

                if (chunk.Coords == coordinates) { chunk.DerivedRevisions.InvalidateHeightsAndLight(); }
                else { chunk.DerivedRevisions.InvalidateLight(); }

                DerivedInputsInvalidated?.Invoke(chunk);
                if (_disposed || !WorldRuntimeContext.IsProjectActive(world.Project)) { return; }

                Request(chunk);
            }
        }
    }

    public void InvalidateSkylight()
    {
        if (_disposed || !WorldRuntimeContext.IsProjectActive(world.Project)) { return; }

        foreach (var chunk in world.Terrain.AllocatedChunks)
        {
            chunk.DerivedRevisions.InvalidateLight();
            DerivedInputsInvalidated?.Invoke(chunk);
            if (_disposed || !WorldRuntimeContext.IsProjectActive(world.Project)) { return; }

            Request(chunk);
        }
    }

    public void UpdateDemand(TerrainUpdateLocation[] locations)
    {
        _locations = locations.ToArray();
        foreach (var chunk in world.Terrain.AllocatedChunks)
        {
            if (!IsNeeded(chunk)) { _queue.Cancel(chunk); }
        }

        foreach (var chunk in world.Terrain.AllocatedChunks) { Request(chunk); }
    }

    public void ContentAvailable(TerrainChunk chunk)
    {
        chunk.DerivedRevisions.InvalidateHeightsAndLight();
        foreach (var neighbor in Neighbors(chunk))
        {
            if (!ReferenceEquals(neighbor, chunk) && neighbor.HasContents)
            {
                neighbor.DerivedRevisions.InvalidateLight();
                Request(neighbor);
            }
        }

        _queue.Signal(new TerrainWorkKey(chunk, TerrainWorkStage.Content));
        Request(chunk);
    }

    public void Retired(TerrainChunk chunk)
    {
        _queue.Cancel(chunk);
        _pendingInitialization.TryRemove(new TerrainWorkKey(chunk, TerrainWorkStage.Content), out _);
    }

    public void Renewed(TerrainChunk chunk, ulong previousGeneration)
    {
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
        world.WorkExecutor.Wake();
    }

    public bool Step()
    {
        ValidatePropagation();
        var item = _queue.Take();
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
            if (_propagation?.Key == item.Key) { CancelActivePropagation(); }

            throw;
        }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(started);
            _queue.RecordCost(priority, elapsed);
        }
    }

    public void PrepareSynchronously(IReadOnlyList<TerrainChunk> chunks, TimeSpan maximumTime)
    {
        foreach (var chunk in chunks) { Request(chunk, TerrainWorkPriority.Critical); }

        var started = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(started) < maximumTime &&
               chunks.Any(chunk => chunk.HasContents && !chunk.HasCurrentLight))
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

                var generation = world.AuthorityContents?.Generation;
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
                    lighting.CalculateHeights(chunk);
                    RecordChanges();
                }

                Complete(item, chunk.DerivedRevisions.PublishHeights(request));
                break;
            case TerrainWorkStage.Sunlight:
                if (chunk.DerivedRevisions.HasCurrentSunlight)
                {
                    Complete(item, true);
                    break;
                }

                if (!chunk.DerivedRevisions.HasCurrentSunlight)
                {
                    lighting.ResetSunlight(chunk, world.Project.FindSubsystem<SubsystemSky>(true)!.SkyLightValue);
                    RecordChanges();
                }

                Complete(item, chunk.DerivedRevisions.PublishSunlight(request));
                break;
            case TerrainWorkStage.Propagation:
                AdvancePropagation(item, request);
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

    private void AdvancePropagation(TerrainWorkQueue.Item item, TerrainDerivedRevisions.Request request)
    {
        var chunk = item.Key.Chunk;
        if (chunk.DerivedRevisions.HasCompletedLightInputs)
        {
            Complete(item, true);
            return;
        }

        if (_propagation is { } active && active.Key != item.Key)
        {
            _queue.Wait(item, [active.Key]);
            return;
        }

        if (_propagation == null)
        {
            var inputs = Neighbors(chunk).Where(n => n.HasContents).Select(n => new Input(
                    n,
                    n.AllocationGeneration,
                    ContentVersion(n),
                    n.DerivedRevisions.Capture(),
                    n.DerivedRevisions.HeightRevision
                )
            ).ToArray();
            _propagation = new Propagation(item.Key, request, inputs);
            foreach (var input in inputs) { input.Chunk.DerivedRevisions.BeginLightWrite(); }

            lighting.BeginPropagation(chunk);
            RecordChanges();
        }

        var finished = lighting.AdvancePropagation(chunk, 2048);
        RecordChanges();
        if (!finished)
        {
            _queue.Finish(item, false);
            return;
        }

        var completed = chunk.DerivedRevisions.PublishLight(_propagation.Request);
        EndPropagation();
        Complete(item, completed);
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

        if (key.Stage == TerrainWorkStage.Sunlight) { yield break; }

        if (!chunk.DerivedRevisions.HasCurrentSunlight)
        {
            yield return new TerrainWorkKey(chunk, TerrainWorkStage.Sunlight);
        }

        if (key.Stage == TerrainWorkStage.Propagation)
        {
            foreach (var neighbor in Neighbors(chunk).Where(n => n.HasContents && !n.NeedsContentValidation))
            {
                if (!neighbor.DerivedRevisions.HasCurrentSunlight)
                {
                    yield return new TerrainWorkKey(neighbor, TerrainWorkStage.Sunlight);
                }
            }

            yield break;
        }

        if (_propagation is { } active &&
            active.Inputs.Any(input => Math.Abs(input.Chunk.Coords.X - chunk.Coords.X) <= 1 &&
                                       Math.Abs(input.Chunk.Coords.Y - chunk.Coords.Y) <= 1
            )) { yield return active.Key; }

        foreach (var neighbor in Neighbors(chunk).Where(n => n.HasContents))
        {
            if (neighbor.NeedsContentValidation)
            {
                yield return new TerrainWorkKey(neighbor, TerrainWorkStage.Content);
            }
            else if (!neighbor.DerivedRevisions.HasCompletedLightInputs)
            {
                yield return new TerrainWorkKey(neighbor, TerrainWorkStage.Propagation);
            }
        }
    }

    private TerrainWorkStage? NextStage(TerrainChunk chunk)
    {
        if (!chunk.HasContents || chunk.NeedsContentValidation) { return TerrainWorkStage.Content; }

        if (!chunk.DerivedRevisions.HasCurrentHeights) { return TerrainWorkStage.Heights; }

        if (!chunk.DerivedRevisions.HasCurrentSunlight) { return TerrainWorkStage.Sunlight; }

        if (!chunk.DerivedRevisions.HasCompletedLightInputs) { return TerrainWorkStage.Propagation; }

        return _derived != null && IsVisible(chunk) && !_derived.HasCurrentGeometry(chunk)
            ? TerrainWorkStage.Derived
            : null;
    }

    private void ValidatePropagation()
    {
        if (_propagation is not { } active) { return; }

        if (active.Key.IsCurrent &&
            (IsNeeded(active.Key.Chunk) || _queue.HasDependents(active.Key.Chunk)) &&
            active.Inputs.All(input => !input.Chunk.IsDisposed &&
                                       input.Chunk.AllocationGeneration == input.Generation &&
                                       ContentVersion(input.Chunk) == input.Contents &&
                                       input.Chunk.DerivedRevisions.Capture() == input.Request &&
                                       input.Chunk.DerivedRevisions.HeightRevision == input.Heights
            )) { return; }

        CancelActivePropagation();
    }

    private void CancelActivePropagation()
    {
        var active = _propagation!;
        // A cancelled flood has already written part of its frontier. A new flood must
        // rebuild base light, or the now-equal seed values would silently lose that frontier.
        RecordChanges();
        lighting.CancelPropagation();
        foreach (var input in active.Inputs) { input.Chunk.DerivedRevisions.InvalidateLight(); }

        EndPropagation();
        _queue.Signal(active.Key);
        foreach (var input in active.Inputs) { Request(input.Chunk); }
    }

    private void EndPropagation()
    {
        var active = _propagation!;
        _propagation = null;
        foreach (var input in active.Inputs)
        {
            input.Chunk.DerivedRevisions.EndLightWrite();
            if (input.Chunk.HasCurrentLight)
            {
                QueueInitialization(input.Chunk);
                Request(input.Chunk);
            }
        }
    }

    private void RecordChanges()
    {
        foreach (var changed in lighting.ChangedChunks)
        {
            changed.DerivedRevisions.RecordLightChange();
            foreach (var neighbor in Neighbors(changed))
            {
                if (neighbor.HasCurrentLight) { Request(neighbor); }
            }
        }
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
                if (world.Terrain.GetChunkAtCoords(chunk.Coords.X + x, chunk.Coords.Y + z) is { } neighbor)
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

    private long ContentVersion(TerrainChunk chunk) => world.ContentRole == TerrainContentRole.Authority
        ? chunk.NetworkContentRevision
        : chunk.NetworkContentVersion;

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
        _disposed = true;
        if (_propagation != null) { CancelActivePropagation(); }

        _queue.Clear();
        _pendingInitialization.Clear();
        _initialization.Clear();
    }
}
