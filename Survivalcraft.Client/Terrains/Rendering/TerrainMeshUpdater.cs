using System.Collections.Concurrent;

using Game.Terrains.Distribution;
using Game.Terrains.Execution;

namespace Game.Terrains.Rendering;

/// <summary>由客户端持有的网格任务及上传前置条件，与 CPU 地形就绪状态一同调度。</summary>
public sealed class TerrainMeshUpdater : ITerrainDerivedWork, IDisposable
{
    private readonly SubsystemTerrainPresentation _presentation;

    private readonly SubsystemTerrain _subsystemTerrain;

    private readonly SubsystemAnimatedTextures _subsystemAnimatedTextures;

    private readonly Terrain _terrain;

    private readonly ConcurrentDictionary<TerrainWorkKey, double> _deferred = new();

    private bool _disposed;

    private long _lastSynchronousRevision = -1;

    private TerrainMainWorkRequest? _interactionRequest;

    public TerrainMeshUpdater(SubsystemTerrainPresentation presentation)
    {
        _presentation = presentation;
        _subsystemTerrain = presentation.TerrainSubsystem;
        _subsystemAnimatedTextures = presentation.SubsystemAnimatedTextures;
        _terrain = _subsystemTerrain.Terrain;
        _subsystemTerrain.Computations.AttachDerivedWork(this);
    }

    public void Update()
    {
        if (_disposed) { return; }

        foreach (var item in _deferred)
        {
            if (!item.Key.IsCurrent || !_subsystemTerrain.Computations.IsDerivedRequested(item.Key.Chunk))
            {
                _deferred.TryRemove(item.Key, out _);
                continue;
            }

            // Retain the due entry until the worker consumes it: an early signal cannot lose a wakeup.
            if (Time.RealTime >= item.Value ||
                _presentation.ChunkPresentations.TryGet(item.Key.Chunk, out var mesh) &&
                mesh.NeedsGroupedLighting(item.Key.Chunk))
            {
                _subsystemTerrain.Computations.ResumeDerived(item.Key.Chunk, item.Key.Generation);
            }
        }

        _subsystemTerrain.WorkExecutor.Wake();
    }


    bool ITerrainDerivedWork.HasCurrentGeometry(TerrainChunk chunk) =>
        _presentation.ChunkPresentations.Get(chunk).HasCurrentGeometry(chunk);

    bool ITerrainDerivedWork.HasUploadedGeometry(TerrainChunk chunk) =>
        _presentation.ChunkPresentations.Get(chunk).GeometryUploaded;

    bool ITerrainDerivedWork.HasPendingLightingPresentation(TerrainChunk chunk)
    {
        return _presentation.ChunkPresentations.TryGet(chunk, out var mesh) && mesh.NeedsGroupedLighting(chunk);
    }

    bool ITerrainDerivedWork.HasPriorityLightingPresentation(TerrainChunk chunk) =>
        _presentation.ChunkPresentations.TryGet(chunk, out var mesh) && mesh.lightingPresentationPriority;

    bool ITerrainDerivedWork.CanUpdateBeforeLighting(TerrainChunk chunk) =>
        NeedsInteractiveMesh(chunk) && _presentation.ChunkPresentations.Get(chunk).CanUsePublishedLighting(chunk);

    TerrainDerivedWorkResult ITerrainDerivedWork.UpdateChunk(TerrainChunk chunk)
    {
        var key = new TerrainWorkKey(chunk, TerrainWorkStage.Derived);
        _deferred.TryRemove(key, out _);
        var presentation = _presentation.ChunkPresentations.Get(chunk);
        if (!presentation.NeedsGroupedLighting(chunk) &&
            !_subsystemTerrain.Computations.IsInteractive(chunk) &&
            presentation.ShouldDeferMaintenance(chunk, Time.RealTime, out var due))
        {
            _deferred[key] = due;
            return TerrainDerivedWorkResult.Waiting;
        }

        var interactive = _subsystemTerrain.Computations.IsInteractive(chunk);
        var preview = interactive && presentation.CanUsePublishedLighting(chunk);
        UpdateChunkMesh(chunk, preview, TimeSpan.FromMilliseconds(4), interactive);
        return presentation.HasCurrentGeometry(chunk)
            ? TerrainDerivedWorkResult.Complete
            : TerrainDerivedWorkResult.Continue;
    }

    public void PrepareForDrawing(Camera camera)
    {
        _subsystemTerrain.SetUpdateLocation(
            camera.GameWidget.CharacterData.CharacterIndex,
            camera.ViewPosition.XZ,
            Configuration.ClientConfiguration.Preferences.VisibilityRange,
            64f
        );
        var revision = _subsystemTerrain.TerrainInteractionRevision;
        if (_lastSynchronousRevision == revision &&
            !_subsystemTerrain.Computations.HasInteractiveWork)
        {
            _interactionRequest?.Dispose();
            _interactionRequest = null;
            return;
        }

        var chunks = DetermineSynchronousUpdateChunks(camera.ViewPosition, camera.ViewDirection);
        if (chunks.Count == 0)
        {
            _interactionRequest?.Dispose();
            _interactionRequest = null;
            _lastSynchronousRevision = revision;
            return;
        }

        var dependencies = chunks.SelectMany(NeighborChunks).Distinct().ToArray();
        var started = Stopwatch.GetTimestamp();
        _interactionRequest ??= _subsystemTerrain.WorkExecutor.CreateMainRequest();
        if (!_interactionRequest.TryEnter(1)) { return; }

        try
        {
            _subsystemTerrain.Computations.PrepareHeightsSynchronously(dependencies, TimeSpan.FromMilliseconds(2));
            var complete = true;
            foreach (var chunk in chunks)
            {
                var presentation = _presentation.ChunkPresentations.Get(chunk);
                var interactive = _subsystemTerrain.Computations.IsInteractive(chunk);
                var pendingLighting = !chunk.HasCurrentLight ||
                                      NeighborChunks(chunk).Any(neighbor => !neighbor.HasCurrentLight);
                if (pendingLighting && (!interactive || !presentation.CanUsePublishedLighting(chunk)))
                {
                    complete = false;
                    continue;
                }

                while (NeedsInteractiveMesh(chunk) &&
                       Stopwatch.GetElapsedTime(started) < TimeSpan.FromMilliseconds(4))
                {
                    var remaining = TimeSpan.FromMilliseconds(4) - Stopwatch.GetElapsedTime(started);
                    if (remaining <= TimeSpan.Zero) { break; }

                    UpdateChunkMesh(
                        chunk,
                        interactive,
                        remaining,
                        interactive
                    );
                }

                complete &= !NeedsInteractiveMesh(chunk);
            }

            if (complete) { _lastSynchronousRevision = revision; }
        }
        finally
        {
            _interactionRequest.Leave();
            _interactionRequest.Dispose();
            _interactionRequest = null;
        }
    }

    private List<TerrainChunk> DetermineSynchronousUpdateChunks(Vector3 viewPosition, Vector3 viewDirection)
    {
        var vector = Vector3.Normalize(Vector3.Cross(viewDirection, Vector3.UnitY));
        var v = Vector3.Normalize(Vector3.Cross(viewDirection, vector));
        var obj = new[]
        {
            viewPosition,
            viewPosition + 6f * viewDirection,
            viewPosition + 6f * viewDirection - 6f * vector,
            viewPosition + 6f * viewDirection + 6f * vector,
            viewPosition + 6f * viewDirection - 2f * v,
            viewPosition + 6f * viewDirection + 2f * v
        };
        var list = _subsystemTerrain.Computations.GetInteractiveChunks()
            .Where(chunk => !chunk.IsDisposed &&
                            chunk.HasContents &&
                            NeedsInteractiveMesh(chunk)
            )
            .OrderBy(chunk => Vector2.DistanceSquared(chunk.Center, viewPosition.XZ)).ToList();
        foreach (var samplePosition in obj)
        {
            var chunkAtCell = _terrain.GetChunkAtCell(
                Terrain.ToCell(samplePosition.X),
                Terrain.ToCell(samplePosition.Z),
                false
            );
            if (chunkAtCell != null &&
                !list.Contains(chunkAtCell) &&
                (!chunkAtCell.HasCurrentLight ||
                 (_presentation.ChunkPresentations is { } presentations &&
                  !presentations.Get(chunkAtCell).HasCurrentGeometry(chunkAtCell))))
            {
                list.Add(chunkAtCell);
            }
        }

        return list;
    }

    private bool NeedsInteractiveMesh(TerrainChunk chunk)
    {
        var mesh = _presentation.ChunkPresentations.Get(chunk);
        return !mesh.HasCurrentGeometry(chunk) ||
               !mesh.HasCurrentUploadedGeometry(chunk) && !mesh.CanUploadGeometry(chunk);
    }

    private IEnumerable<TerrainChunk> NeighborChunks(TerrainChunk target)
    {
        for (var x = -1; x <= 1; x++)
        {
            for (var z = -1; z <= 1; z++)
            {
                if (_terrain.GetChunkAtCoords(target.Coords.X + x, target.Coords.Y + z) is { HasContents: true } chunk)
                {
                    yield return chunk;
                }
            }
        }
    }

    public void RequestAllMeshesRebuild(bool forceRegeneration)
    {
        foreach (var chunk in _terrain.AllocatedChunks)
        {
            var presentation = _presentation.ChunkPresentations.Get(chunk);
            presentation.RequestMeshRebuild();
            _subsystemTerrain.Computations.Request(chunk);
            if (forceRegeneration) { presentation.InvalidateSliceContentsHashes(); }
        }

        Update();
    }

    public void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        _interactionRequest?.Dispose();
        _interactionRequest = null;
        _subsystemTerrain.Computations.DetachDerivedWork(this);
        _deferred.Clear();
    }

    public float GetUpdateProgress(int locationIndex, float visibilityDistance, float contentDistance)
    {
        var validChunkCount = 0;
        var invalidChunkCount = 0;
        if (!_subsystemTerrain.Demands.Locations.TryGetValue(locationIndex, out var location))
        {
            return 0f;
        }

        visibilityDistance = MathUtils.Max(
            MathUtils.Min(visibilityDistance, location.VisibilityDistance) - 8f - 0.1f,
            0f
        );

        contentDistance = MathUtils.Max(
            MathUtils.Min(contentDistance, location.ContentDistance) - 8f - 0.1f,
            0f
        );

        var visibilityDistanceSqr = MathUtils.Sqr(visibilityDistance);
        var contentDistanceSqr = MathUtils.Sqr(contentDistance);

        var distanceMax = MathUtils.Max(visibilityDistance, contentDistance);

        var chunkLocationMin = Terrain.ToChunk(location.Center - new Vector2(distanceMax));
        var chunkLocationMax = Terrain.ToChunk(location.Center + new Vector2(distanceMax));

        for (var i = chunkLocationMin.X; i <= chunkLocationMax.X; i++)
        {
            for (var j = chunkLocationMin.Y; j <= chunkLocationMax.Y; j++)
            {
                var chunk = _terrain.GetChunkAtCoords(i, j);
                var distanceSqrFromChunkToLocation = Vector2.DistanceSquared(
                    v2: new Vector2((i + 0.5f) * 16f, (j + 0.5f) * 16f),
                    v1: location.Center
                );
                if (distanceSqrFromChunkToLocation <= visibilityDistanceSqr)
                {
                    if (chunk == null ||
                        !chunk.HasCurrentLight ||
                        (_presentation.ChunkPresentations is { } presentations &&
                         !presentations.Get(chunk).HasCurrentGeometry(chunk)))
                    {
                        invalidChunkCount++;
                    }
                    else
                    {
                        validChunkCount++;
                    }
                }
                else if (distanceSqrFromChunkToLocation <= contentDistanceSqr)
                {
                    if (chunk is not { HasContents: true })
                    {
                        invalidChunkCount++;
                    }
                    else
                    {
                        validChunkCount++;
                    }
                }
            }
        }

        if (invalidChunkCount > 0)
        {
            return validChunkCount / (float)(invalidChunkCount + validChunkCount);
        }

        return 1f;
    }

    private void UpdateChunkMesh(
        TerrainChunk chunk,
        bool allowPendingLighting,
        TimeSpan maximumTime,
        bool interactive = false
    )
    {
        var presentation = _presentation.ChunkPresentations.Get(chunk);
        if (presentation.NeedsGroupedLighting(chunk) && !(interactive && presentation.CanPreviewDisplayedLight(chunk)))
        {
            allowPendingLighting = false;
            interactive = false;
        }

        if (!chunk.HasCurrentLight &&
            !(allowPendingLighting && presentation.CanUsePublishedLighting(chunk))) { return; }

        lock (presentation.Geometry)
        {
            if (presentation.PrepareMeshUpdate(chunk, allowPendingLighting) != TerrainMeshState.Building) { return; }

            presentation.NewGeometryData = false;
            var started = Stopwatch.GetTimestamp();
            var builder = new TerrainMeshBuilder(
                _terrain,
                _presentation.BlockGeometryGenerator,
                _subsystemAnimatedTextures.AnimatedBlocksTexture
            );
            while (presentation.CompletedMeshSlices < TerrainChunk.SlicesCount)
            {
                if (interactive &&
                    _subsystemTerrain.Computations.IsInteractive(chunk) &&
                    presentation.TryReuseInteractiveSlice(presentation.NextMeshSlice))
                {
                    presentation.CompleteMeshSlice();
                    continue;
                }

                var remaining = maximumTime - Stopwatch.GetElapsedTime(started);
                if (remaining <= TimeSpan.Zero ||
                    !builder.GenerateSlice(chunk, presentation, presentation.NextMeshSlice, remaining))
                {
                    return;
                }

                presentation.CompleteMeshSlice();
            }

            if (presentation.CompletedMeshSlices == TerrainChunk.SlicesCount &&
                ClientChunkDerivationPipeline.CompleteGeometry(chunk, presentation))
            {
                var key = new TerrainWorkKey(chunk, TerrainWorkStage.Derived);
                if (_deferred.TryRemove(key, out _))
                {
                    _subsystemTerrain.Computations.ResumeDerived(chunk, key.Generation);
                }
            }
        }
    }
}
