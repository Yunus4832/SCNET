using Game.Terrains.Distribution;

namespace Game.Terrains.Rendering;

/// <summary>Client-owned mesh tasks and upload prerequisites, scheduled with CPU terrain readiness.</summary>
public sealed class TerrainMeshUpdater : ITerrainDerivedWork, IDisposable
{
    private readonly SubsystemTerrainPresentation _presentation;

    private readonly SubsystemTerrain _subsystemTerrain;

    private readonly SubsystemAnimatedTextures _subsystemAnimatedTextures;

    private readonly Terrain _terrain;

    private bool _disposed;

    private long _lastSynchronousRevision = -1;

    public TerrainMeshUpdater(SubsystemTerrainPresentation presentation)
    {
        _presentation = presentation;
        _subsystemTerrain = presentation.TerrainSubsystem;
        _subsystemAnimatedTextures = presentation.SubsystemAnimatedTextures;
        _terrain = _subsystemTerrain.Terrain;
        _subsystemTerrain.TerrainUpdater.AttachDerivedWork(this);
    }

    public void Update() => _subsystemTerrain.TerrainUpdater.RequestDerivedWorkUpdate();

    TerrainChunk? ITerrainDerivedWork.SelectChunk(TerrainChunk[] chunks, IEnumerable<TerrainUpdateLocation> locations)
    {
        return SelectMeshChunkToUpdate(chunks, locations, _presentation.ChunkPresentations);
    }

    TerrainChunk? ITerrainDerivedWork.GetCpuDependency(TerrainChunk chunk)
    {
        return ClientDerivedTerrainPolicy.FindPendingGeometryDependency(_terrain, TerrainContentRole.Replica, chunk);
    }

    void ITerrainDerivedWork.UpdateChunk(TerrainChunk chunk) => UpdateChunkMesh(chunk);

    public void PrepareForDrawing(Camera camera)
    {
        var updater = _subsystemTerrain.TerrainUpdater;
        updater.SetUpdateLocation(
            camera.GameWidget.CharacterData.CharacterIndex,
            camera.ViewPosition.XZ,
            Configuration.ClientConfiguration.Preferences.VisibilityRange,
            64f
        );
        var revision = updater.SynchronousUpdateRevision;
        if (_lastSynchronousRevision == revision) { return; }

        _lastSynchronousRevision = revision;
        var chunks = DetermineSynchronousUpdateChunks(camera.ViewPosition, camera.ViewDirection);
        var dependencies = chunks.SelectMany(NeighborChunks).Distinct().ToArray();
        updater.EnsureCpuReady(dependencies);
        updater.UpdateEvent.WaitOne();
        try
        {
            foreach (var chunk in chunks)
            {
                if (chunk.WorkerState != TerrainChunkState.Ready) { continue; }

                while (!_presentation.ChunkPresentations.Get(chunk).HasCurrentGeometry(chunk))
                {
                    if (ClientDerivedTerrainPolicy.FindPendingGeometryDependency(
                            _terrain,
                            _subsystemTerrain.ContentRole,
                            chunk
                        ) !=
                        null) { break; }

                    UpdateChunkMesh(chunk);
                }
            }
        }
        finally { updater.UpdateEvent.Set(); }
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
        var list = new List<TerrainChunk>();
        foreach (var vector2 in obj)
        {
            var chunkAtCell = _terrain.GetChunkAtCell(Terrain.ToCell(vector2.X), Terrain.ToCell(vector2.Z), false);
            if (chunkAtCell != null &&
                !list.Contains(chunkAtCell) &&
                (chunkAtCell.MainThreadState < TerrainChunkState.Ready ||
                 (_presentation.ChunkPresentations is { } presentations &&
                  !presentations.Get(chunkAtCell).HasCurrentGeometry(chunkAtCell))))
            {
                list.Add(chunkAtCell);
            }
        }

        return list;
    }

    private IEnumerable<TerrainChunk> NeighborChunks(TerrainChunk target)
    {
        for (var x = -1; x <= 1; x++)
        {
            for (var z = -1; z <= 1; z++)
            {
                if (_terrain.GetChunkAtCoords(target.Coords.X + x, target.Coords.Y + z) is { IsLoaded: true } chunk)
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
            if (forceRegeneration) { presentation.InvalidateSliceContentsHashes(); }
        }

        Update();
    }

    public void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        _subsystemTerrain.TerrainUpdater.DetachDerivedWork(this);
    }

    public float GetUpdateProgress(int locationIndex, float visibilityDistance, float contentDistance)
    {
        var validChunkCount = 0;
        var invalidChunkCount = 0;
        if (!_subsystemTerrain.TerrainUpdater.UpdateLocations.TryGetValue(locationIndex, out var location))
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
                        chunk.MainThreadState < TerrainChunkState.Ready ||
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
                    if (chunk == null || chunk.MainThreadState < TerrainChunkState.InvalidLight)
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

    internal static bool IsNetworkChunkStalled(TerrainChunk chunk, double now, TerrainChunkPresentation presentation)
    {
        if (!chunk.IsLoaded || chunk.NeedsContentSnapshot)
        {
            return chunk.IsRequested &&
                   now - chunk.NetworkRequestTime >= TerrainUpdater.NetworkChunkRequestRetrySeconds;
        }

        if (chunk.NetworkContentReceiveTime <= 0.0 ||
            now - chunk.NetworkContentReceiveTime < TerrainUpdater.NetworkChunkRequestRetrySeconds)
        {
            return false;
        }

        // Lighting or neighbor invalidation may rebuild derived data while the currently
        // uploaded geometry for the same authoritative contents remains visible.
        if (ClientChunkDerivationPipeline.CanDraw(TerrainContentRole.Replica, chunk, presentation))
        {
            return false;
        }

        return chunk.WorkerState < TerrainChunkState.Ready ||
               chunk.MainThreadState < TerrainChunkState.Ready ||
               !presentation.GeometryUploaded;
    }

    internal static TerrainChunk? SelectMeshChunkToUpdate(
        TerrainChunk[] chunks,
        IEnumerable<TerrainUpdateLocation> locations,
        TerrainChunkPresentationStore presentations
    )
    {
        TerrainChunk? result = null;
        var nearest = float.MaxValue;
        foreach (var chunk in chunks)
        {
            if (chunk.WorkerState != TerrainChunkState.Ready || presentations.Get(chunk).HasCurrentGeometry(chunk))
            {
                continue;
            }

            foreach (var location in locations)
            {
                var distance = Vector2.DistanceSquared(location.Center, chunk.Center);
                if (distance <= MathUtils.Sqr(location.VisibilityDistance) && distance < nearest)
                {
                    nearest = distance;
                    result = chunk;
                }
            }
        }

        return result;
    }

    private void UpdateChunkMesh(TerrainChunk chunk)
    {
        if (chunk.WorkerState != TerrainChunkState.Ready)
        {
            throw new InvalidOperationException("Meshes require CPU-ready terrain.");
        }

        var dependency = ClientDerivedTerrainPolicy.FindPendingGeometryDependency(
            _terrain,
            _subsystemTerrain.ContentRole,
            chunk
        );
        if (dependency != null)
        {
            // The independent CPU worker owns this dependency; never wait while holding its token.
            return;
        }

        var presentation = _presentation.ChunkPresentations.Get(chunk);
        lock (presentation.Geometry)
        {
            switch (presentation.PrepareMeshUpdate(chunk))
            {
                case TerrainMeshState.NeedsFirstPass:
                    CalculateChunkSliceContentsHash(chunk);
                    presentation.NewGeometryData = false;
                    GenerateChunkVertices(chunk, true);
                    presentation.CompleteFirstPass();
                    break;
                case TerrainMeshState.NeedsSecondPass:
                    GenerateChunkVertices(chunk, false);
                    if (ClientChunkDerivationPipeline.CompleteGeometry(chunk, presentation))
                    {
                        presentation.NetworkGeometryReadyTime = Time.RealTime;
                    }

                    break;
            }
        }
    }

    private void GenerateChunkVertices(TerrainChunk chunk, bool even)
    {
        _presentation.BlockGeometryGenerator.ResetCache();
        if (!_presentation.ChunkPresentations.Get(chunk).Draws.TryGetValue(
                _subsystemAnimatedTextures.AnimatedBlocksTexture,
                out var terrainGeometry
            ))
        {
            terrainGeometry = new TerrainGeometry[TerrainChunk.SlicesCount];
            for (var i = 0; i < TerrainChunk.SlicesCount; i++)
            {
                var t = new TerrainGeometry(_presentation.ChunkPresentations.Get(chunk).Draws, i);
                terrainGeometry[i] = t;
            }

            _presentation.ChunkPresentations.Get(chunk).Draws.Add(
                _subsystemAnimatedTextures.AnimatedBlocksTexture,
                terrainGeometry
            );
        }

        var chunkAtCoords = _terrain.GetChunkAtCoords(chunk.Coords.X - 1, chunk.Coords.Y - 1);
        var chunkAtCoords2 = _terrain.GetChunkAtCoords(chunk.Coords.X, chunk.Coords.Y - 1);
        var chunkAtCoords3 = _terrain.GetChunkAtCoords(chunk.Coords.X + 1, chunk.Coords.Y - 1);
        var chunkAtCoords4 = _terrain.GetChunkAtCoords(chunk.Coords.X - 1, chunk.Coords.Y);
        var chunkAtCoords5 = _terrain.GetChunkAtCoords(chunk.Coords.X + 1, chunk.Coords.Y);
        var chunkAtCoords6 = _terrain.GetChunkAtCoords(chunk.Coords.X - 1, chunk.Coords.Y + 1);
        var chunkAtCoords7 = _terrain.GetChunkAtCoords(chunk.Coords.X, chunk.Coords.Y + 1);
        var chunkAtCoords8 = _terrain.GetChunkAtCoords(chunk.Coords.X + 1, chunk.Coords.Y + 1);
        var num = 0;
        var num2 = 0;
        var num3 = 16;
        var num4 = 16;
        if (chunkAtCoords4 == null)
        {
            num++;
        }

        if (chunkAtCoords2 == null)
        {
            num2++;
        }

        if (chunkAtCoords5 == null)
        {
            num3--;
        }

        if (chunkAtCoords7 == null)
        {
            num4--;
        }

        for (var i = 0; i < TerrainChunk.SlicesCount; i++)
        {
            if (i >= 17)
            {
            }

            if (i % 2 == 0 != even)
            {
                continue;
            }

            _presentation.ChunkPresentations.Get(chunk).SliceContentsHashes[i] =
                CalculateChunkSliceContentsHash(chunk, i);
            var presentation = _presentation.ChunkPresentations.Get(chunk);
            var contentsHash = presentation.SliceContentsHashes[i];
            if (!presentation.BeginSliceGeometryUpdate(i, contentsHash))
            {
                continue;
            }

            foreach (var c in _presentation.ChunkPresentations.Get(chunk).Draws)
            {
                var subsets = c.Value[i].Subsets;
                foreach (var subset in subsets)
                {
                    subset.Vertices.Clear();
                    subset.Indices.Clear();
                }
            }

            for (var k = num; k < num3; k++)
            {
                for (var l = num2; l < num4; l++)
                {
                    switch (k)
                    {
                        case 0:
                            if ((l == 0 && chunkAtCoords == null) || (l == 15 && chunkAtCoords6 == null))
                            {
                                continue;
                            }

                            break;
                        case 15:
                            if ((l == 0 && chunkAtCoords3 == null) || (l == 15 && chunkAtCoords8 == null))
                            {
                                continue;
                            }

                            break;
                    }

                    var num5 = k + chunk.Origin.X;
                    var num6 = l + chunk.Origin.Y;
                    var bottomHeightFast = chunk.GetBottomHeightFast(k, l);
                    var bottomHeight = _terrain.GetBottomHeight(num5 - 1, num6);
                    var bottomHeight2 = _terrain.GetBottomHeight(num5 + 1, num6);
                    var bottomHeight3 = _terrain.GetBottomHeight(num5, num6 - 1);
                    var bottomHeight4 = _terrain.GetBottomHeight(num5, num6 + 1);
                    var x = MathUtils.Min(
                        bottomHeightFast - 1,
                        MathUtils.Min(bottomHeight, bottomHeight2, bottomHeight3, bottomHeight4)
                    );
                    var x2 = chunk.GetTopHeightFast(k, l) + 1;
                    var num7 = MathUtils.Max(16 * i, x, 1);
                    var num8 = MathUtils.Min(16 * (i + 1), x2, 256);
                    var num9 = TerrainChunk.CalculateCellIndex(k, 0, l);
                    for (var m = num7; m < num8; m++)
                    {
                        var cellValueFast = chunk.GetCellValueForGeometry(num9 + m);
                        var num10 = Terrain.ExtractContents(cellValueFast);
                        if (num10 != 0)
                        {
                            _presentation.BlockGeometryGenerator.Presentations.Get(num10).GenerateTerrainVertices(
                                _presentation.BlockGeometryGenerator,
                                terrainGeometry[i],
                                cellValueFast,
                                num5,
                                m,
                                num6
                            );
                        }
                    }
                }
            }

            presentation.CompleteSliceGeometryUpdate(i, contentsHash);
        }
    }

    private int CalculateChunkSliceContentsHash(TerrainChunk chunk, int sliceIndex)
    {
        var num = 1;
        var num2 = chunk.Origin.X - 1;
        var num3 = chunk.Origin.X + 16 + 1;
        var num4 = chunk.Origin.Y - 1;
        var num5 = chunk.Origin.Y + 16 + 1;
        var x = MathUtils.Max(16 * sliceIndex - 1, 0);
        var x2 = MathUtils.Min(16 * (sliceIndex + 1) + 1, 256);
        for (var i = num2; i < num3; i++)
        {
            for (var j = num4; j < num5; j++)
            {
                var chunkAtCell = _terrain.GetChunkAtCell(i, j, false);
                if (chunkAtCell == null)
                {
                    continue;
                }

                var x3 = i & 0xF;
                var z = j & 0xF;
                var shaftValueFast = chunkAtCell.GetShaftValueFast(x3, z);
                var num6 = Terrain.ExtractBottomHeight(shaftValueFast);
                var num7 = Terrain.ExtractTopHeight(shaftValueFast);
                var num8 = MathUtils.Max(x, num6 - 1);
                var num9 = MathUtils.Min(x2, num7 + 2);
                var num10 = TerrainChunk.CalculateCellIndex(x3, num8, z);
                var num11 = num10 + num9 - num8;
                while (num10 < num11)
                {
                    num += chunkAtCell.GetCellValueForGeometry(num10++);
                    num *= 31;
                }

                num += Terrain.ExtractTemperature(shaftValueFast);
                num *= 31;
                num += Terrain.ExtractHumidity(shaftValueFast);
                num *= 31;
                num += num8;
                num *= 31;
            }
        }

        num += _terrain.SeasonTemperature;
        num *= 31;
        num += _terrain.SeasonHumidity;
        num *= 31;
        return num;
    }

    private void CalculateChunkSliceContentsHash(TerrainChunk chunk)
    {
        var num = 1;
        num += _terrain.SeasonTemperature;
        num *= 31;
        num += _terrain.SeasonHumidity;
        num *= 31;
        for (var i = 0; i < TerrainChunk.SlicesCount; i++)
        {
            _presentation.ChunkPresentations.Get(chunk).SliceContentsHashes[i] = num;
        }

        var num2 = chunk.Origin.X - 1;
        var num3 = chunk.Origin.X + 16 + 1;
        var num4 = chunk.Origin.Y - 1;
        var num5 = chunk.Origin.Y + 16 + 1;
        for (var j = num2; j < num3; j++)
        {
            for (var k = num4; k < num5; k++)
            {
                var chunkAtCell = _terrain.GetChunkAtCell(j, k, false);
                if (chunkAtCell == null)
                {
                    continue;
                }

                var num6 = j & 15;
                var num7 = k & 15;
                var shaftValueFast = chunkAtCell.GetShaftValueFast(num6, num7);
                var num8 = Terrain.ExtractTopHeight(shaftValueFast);
                var num9 = Terrain.ExtractBottomHeight(shaftValueFast);
                var num10 = num6 > 0
                    ? chunkAtCell.GetBottomHeightFast(num6 - 1, num7)
                    : _terrain.GetBottomHeight(j - 1, k);
                var num11 = num7 > 0
                    ? chunkAtCell.GetBottomHeightFast(num6, num7 - 1)
                    : _terrain.GetBottomHeight(j, k - 1);
                var num12 = num6 < 15
                    ? chunkAtCell.GetBottomHeightFast(num6 + 1, num7)
                    : _terrain.GetBottomHeight(j + 1, k);
                var num13 = num7 < 15
                    ? chunkAtCell.GetBottomHeightFast(num6, num7 + 1)
                    : _terrain.GetBottomHeight(j, k + 1);
                var num14 = MathUtils.Min(MathUtils.Min(num10, num11, num12, num13), num9 - 1);
                var num15 = num8 + 2;
                num14 = MathUtils.Max(num14, 0);
                num15 = MathUtils.Min(num15, 256);
                var num16 = MathUtils.Max((num14 - 1) / 16, 0);
                var num17 = MathUtils.Min((num15 + 1) / 16, TerrainChunk.SlicesCount - 1);
                var num18 = 1;
                num18 += Terrain.ExtractTemperature(shaftValueFast);
                num18 *= 31;
                num18 += Terrain.ExtractHumidity(shaftValueFast);
                num18 *= 31;
                for (var l = num16; l <= num17; l++)
                {
                    var num19 = num18;
                    var num20 = MathUtils.Max(l * 16 - 1, num14);
                    var num21 = MathUtils.Min(l * 16 + 16 + 1, num15);
                    var m = TerrainChunk.CalculateCellIndex(num6, num20, num7);
                    var num22 = m + num21 - num20;
                    while (m < num22)
                    {
                        num19 += chunkAtCell.GetCellValueForGeometry(m++);
                        num19 *= 31;
                    }

                    num19 += num20;
                    num19 *= 31;
                    _presentation.ChunkPresentations.Get(chunk).SliceContentsHashes[l] += num19;
                }
            }
        }
    }
}
