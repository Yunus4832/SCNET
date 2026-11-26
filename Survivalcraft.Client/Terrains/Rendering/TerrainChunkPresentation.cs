using Engine.Graphics;

using Game.Terrains.Lighting;

namespace Game.Terrains.Rendering;

/// <summary>客户端为一次区块分配持有的网格和 GPU 资源。</summary>
public sealed class TerrainChunkPresentation : IDisposable
{
    private DynamicArray<TerrainChunkGeometry.Buffer>? _buffers;

    public DynamicArray<TerrainChunkGeometry.Buffer> Buffers => _buffers ??= [];

    public TerrainGeometry[] ChunkSliceGeometries => field ??= new TerrainGeometry[TerrainChunk.SlicesCount];

    public float DrawDistanceSquared;

    private Dictionary<Texture2D, TerrainGeometry[]>? _draws;

    public Dictionary<Texture2D, TerrainGeometry[]> Draws => _draws ??= new();

    // Inputs describe the actual CPU Draws, independently of uploaded GPU buffers.
    internal readonly TerrainSliceInputs[] generatedSliceInputs = new TerrainSliceInputs[TerrainChunk.SlicesCount];

    private readonly bool[] _sliceGeometryValid = new bool[TerrainChunk.SlicesCount];

    private readonly bool[] _sliceTopologyValid = new bool[TerrainChunk.SlicesCount];

    private readonly TerrainMeshLightReads[] _sliceLightReads = new TerrainMeshLightReads[TerrainChunk.SlicesCount];

    private int _recordedLightReads;

    private long _topologyRevision;

    private long _uploadedTopologyRevision;

    public bool HasGeometry => _geometry is not null;

    private TerrainChunkGeometry? _geometry;

    public TerrainChunkGeometry Geometry => _geometry ??= new TerrainChunkGeometry();

    public bool GeometryUploaded;

    internal volatile bool lightingCohortPending;

    internal volatile bool lightingPresentationPriority;

    internal bool NeedsGroupedLighting(TerrainChunk chunk) =>
        lightingCohortPending || chunk.HasPendingLightingPresentation;

    public long ClientGeometryContentVersion => Volatile.Read(ref _completedMesh)?.ContentVersion ?? 0;

    private long _requestedMeshRevision;

    private MeshBuildStamp? _activeMesh;

    private MeshBuildStamp? _completedMesh;

    private MeshBuildStamp? _uploadedMesh;

    private TerrainLightSnapshot?[]? _buildLights;

    private bool _reuseCompletedGeometry;

    private readonly TerrainSliceInputs[] _uploadedSliceInputs = new TerrainSliceInputs[TerrainChunk.SlicesCount];

    internal HashSet<int>? HiddenBlockModels { get; private set; }

    internal void SetBlockModelHidden(int cellIndex, bool hidden)
    {
        if (hidden) { (HiddenBlockModels ??= []).Add(cellIndex); }
        else { HiddenBlockModels?.Remove(cellIndex); }
    }

    private int _interactiveSlices;

    private bool _reusedInteractiveSlices;

    internal void MarkInteractiveCell(int y)
    {
        for (var slice = Math.Max(0, (y - 1) / 16); slice <= Math.Min(15, (y + 1) / 16); slice++)
        {
            _interactiveSlices |= 1 << slice;
        }
    }

    internal void MarkInteractiveSnapshot() => _interactiveSlices = 0xFFFF;

    internal bool TryReuseInteractiveSlice(int slice)
    {
        if (!GeometryUploaded ||
            _interactiveSlices == 0 ||
            (_interactiveSlices & (1 << slice)) != 0 ||
            !_sliceGeometryValid[slice] ||
            generatedSliceInputs[slice] != _uploadedSliceInputs[slice])
        {
            return false;
        }

        _reusedInteractiveSlices = true;
        return true;
    }

    internal bool FinishInteractiveUpload()
    {
        var reused = _reusedInteractiveSlices || Volatile.Read(ref _uploadedMesh) is { AllowPendingLighting: true };
        _reusedInteractiveSlices = false;
        _interactiveSlices = 0;
        return reused;
    }

    public long UploadedContentVersion => Volatile.Read(ref _uploadedMesh)?.ContentVersion ?? 0;

    /// <summary>标识已完成并等待上传的 CPU 网格，调用方按不透明标识使用。</summary>
    internal object? GeometryUploadToken => Volatile.Read(ref _completedMesh);

    internal bool CanReuseUploadedTopology => GeometryUploaded && _topologyRevision == _uploadedTopologyRevision;

    internal bool HasChangedLighting
    {
        get
        {
            for (var i = 0; i < generatedSliceInputs.Length; i++)
            {
                if (generatedSliceInputs[i].Lighting != _uploadedSliceInputs[i].Lighting ||
                    generatedSliceInputs[i].EmissiveMaterial != _uploadedSliceInputs[i].EmissiveMaterial)
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal bool CanPublishUpload(TerrainChunk chunk, object token) =>
        ReferenceEquals(GeometryUploadToken, token) && CanUploadGeometry(chunk);

    internal DynamicArray<TerrainChunkGeometry.Buffer>? ExchangeBuffers(
        DynamicArray<TerrainChunkGeometry.Buffer> buffers
    )
    {
        var previous = _buffers;
        _buffers = buffers;
        return previous;
    }

    public bool HasCurrentUploadedGeometry(TerrainChunk chunk)
    {
        var uploaded = Volatile.Read(ref _uploadedMesh);
        return uploaded != null && Matches(chunk, uploaded);
    }

    /// <summary>上传网格已使用与当前区块相符的光照，可供掉落物取样。</summary>
    internal bool HasCurrentUploadedLighting(TerrainChunk chunk)
    {
        var uploaded = Volatile.Read(ref _uploadedMesh);
        return uploaded is { AllowPendingLighting: false } &&
               chunk.HasCurrentLight &&
               !NeedsGroupedLighting(chunk) &&
               Matches(chunk, uploaded);
    }

    /// <summary>实际 GPU 上传成功后，在持有几何锁时调用。</summary>
    public void MarkUploaded()
    {
        var completed = Volatile.Read(ref _completedMesh);
        if (completed == null)
        {
            throw new InvalidOperationException("Cannot publish an upload without completed terrain geometry.");
        }

        // Neighbor inputs may change during the GPU call. Record what was uploaded;
        // freshness is checked separately and schedules another build when necessary.
        Volatile.Write(ref _uploadedMesh, completed);
        _uploadedTopologyRevision = _topologyRevision;
        Array.Copy(generatedSliceInputs, _uploadedSliceInputs, TerrainChunk.SlicesCount);
        GeometryUploaded = true;
        HiddenBlockModels?.Clear();
    }

    public TerrainMeshState WorkerMeshState { get; private set; }

    public int CompletedMeshSlices { get; private set; }

    internal SliceWork? ActiveSlice { get; set; }

    internal sealed class SliceWork(int slice, TerrainSliceInputs inputs)
    {
        public int Slice { get; } = slice;

        public TerrainSliceInputs Inputs { get; } = inputs;

        public int Column;

        public int CompactionBuffer;

        public TerrainGeometrySubset[]? RelightSubsets;

        public int LightSubsetIndex;

        public int LightRunIndex;

        public int LightVertexIndex;

        public bool CanReplayLighting = true;

        public TerrainMeshLightReads LightReads;
    }

    public int NextMeshSlice
    {
        get
        {
            if (CompletedMeshSlices >= TerrainChunk.SlicesCount) { return TerrainChunk.SlicesCount; }

            // Preserve material registration order across the original even/odd slice traversal.
            var half = TerrainChunk.SlicesCount / 2;
            return CompletedMeshSlices < half ? CompletedMeshSlices * 2 : (CompletedMeshSlices - half) * 2 + 1;
        }
    }


    private readonly record struct NeighborGeometry(
        ulong Generation,
        long ContentVersion,
        long ContentRevision,
        long Heights,
        long? ReplicaHaloRevision
    );

    private readonly record struct NeighborInput(NeighborGeometry Geometry, long Lighting);

    private sealed record MaintenanceWait(MeshBuildStamp Inputs, double FirstSeen, double ChangedAt);

    private MaintenanceWait? _maintenanceWait;

    private sealed record MeshBuildStamp(
        ulong Generation,
        long ContentVersion,
        long Revision,
        NeighborInput[] Neighbors,
        int SeasonTemperature,
        int SeasonHumidity,
        bool AllowPendingLighting
    )
    {
        internal TerrainMeshLightReads LightReads { get; init; }

        internal ulong? SampledLightFingerprint { get; init; }
    }

    internal bool SamplesLighting(TerrainChunk mesh, TerrainLightPresentationBatch.Source source)
    {
        var offset = source.Allocation.Coords - mesh.Coords;
        var uploaded = Volatile.Read(ref _uploadedMesh);
        var completed = Volatile.Read(ref _completedMesh);
        return uploaded?.LightReads.Intersects(offset, source.Sections, source.Boundaries) == true ||
               completed?.LightReads.Intersects(offset, source.Sections, source.Boundaries) == true;
    }

    internal TerrainLightSnapshot?[]? BuildLights => _buildLights;

    public void RequestMeshRebuild()
    {
        Interlocked.Increment(ref _requestedMeshRevision);
    }

    public bool HasCurrentGeometry(TerrainChunk chunk)
    {
        var completed = Volatile.Read(ref _completedMesh);
        return completed is not null && Matches(chunk, completed);
    }

    public bool CanUploadGeometry(TerrainChunk chunk)
    {
        return NewGeometryData &&
               HasCurrentGeometry(chunk) &&
               (chunk.HasCurrentLight ||
                _completedMesh is { AllowPendingLighting: true } && CanUsePublishedLighting(chunk));
    }

    internal bool CanUsePublishedLighting(TerrainChunk chunk)
    {
        if (!GeometryUploaded || !chunk.HasContents || NeedsGroupedLighting(chunk)) { return false; }

        for (var x = -1; x <= 1; x++)
        {
            for (var z = -1; z <= 1; z++)
            {
                var neighbor = chunk.Terrain.GetChunkAtCoords(chunk.Coords.X + x, chunk.Coords.Y + z);
                if (neighbor is not { HasContents: true }) { continue; }

                if (!neighbor.HasCurrentHeightData ||
                    !chunk.Terrain.Lighting.TryGetPublishedSnapshot(neighbor, out _)) { return false; }
            }
        }

        return true;
    }

    private bool Matches(TerrainChunk chunk, MeshBuildStamp stamp)
    {
        return (!stamp.AllowPendingLighting || !NeedsGroupedLighting(chunk)) &&
               stamp.Generation == chunk.AllocationGeneration &&
               stamp.ContentVersion == chunk.NetworkContentVersion &&
               stamp.Revision == Volatile.Read(ref _requestedMeshRevision) &&
               stamp.SeasonTemperature == chunk.Terrain.SeasonTemperature &&
               stamp.SeasonHumidity == chunk.Terrain.SeasonHumidity &&
               MatchesNeighbors(chunk, stamp);
    }

    private static NeighborInput ReadNeighbor(TerrainChunk chunk, int x, int z)
    {
        var neighbor = chunk.Terrain.GetChunkAtCoords(chunk.Coords.X + x, chunk.Coords.Y + z);
        return neighbor is not { HasContents: true }
            ? default
            : new NeighborInput(
                new NeighborGeometry(
                    neighbor.AllocationGeneration,
                    neighbor.NetworkContentVersion,
                    neighbor.NetworkContentRevision,
                    neighbor.DerivedRevisions.HeightRevision,
                    neighbor.ContentOrigin == TerrainContentOrigin.Replica ? neighbor.GeometryHaloRevision : null
                ),
                neighbor.DerivedRevisions.LightRevision
            );
    }

    private static NeighborInput[] CaptureNeighbors(TerrainChunk chunk)
    {
        var result = new NeighborInput[9];
        var index = 0;
        for (var x = -1; x <= 1; x++)
        {
            for (var z = -1; z <= 1; z++) { result[index++] = ReadNeighbor(chunk, x, z); }
        }

        return result;
    }

    private static bool MatchesNeighbors(TerrainChunk chunk, MeshBuildStamp stamp)
    {
        var index = 0;
        var sampledLightingMayHaveChanged = false;
        for (var x = -1; x <= 1; x++)
        {
            for (var z = -1; z <= 1; z++)
            {
                var expected = stamp.Neighbors[index];
                var current = ReadNeighbor(chunk, x, z);
                if (!MatchesGeometry(
                        expected.Geometry,
                        current.Geometry,
                        index,
                        stamp.SampledLightFingerprint.HasValue
                    ))
                {
                    return false;
                }

                if (!stamp.AllowPendingLighting && expected.Lighting != current.Lighting)
                {
                    if (!stamp.SampledLightFingerprint.HasValue) { return false; }

                    sampledLightingMayHaveChanged |= stamp.LightReads.ReadsNeighbor(index);
                }

                index++;
            }
        }

        return !sampledLightingMayHaveChanged ||
               stamp.SampledLightFingerprint == stamp.LightReads.FingerprintCurrent(chunk);
    }

    private static bool LightingInputsReadable(TerrainChunk chunk)
    {
        if (!chunk.HasCurrentLight) { return false; }

        for (var x = -1; x <= 1; x++)
        {
            for (var z = -1; z <= 1; z++)
            {
                var neighbor = chunk.Terrain.GetChunkAtCoords(chunk.Coords.X + x, chunk.Coords.Y + z);
                if (neighbor is { HasContents: true, HasCurrentLight: false }) { return false; }
            }
        }

        return true;
    }

    /// <summary>合并已可见区块的维护任务，同时允许首次几何生成和有效的分片构建继续推进。</summary>
    internal bool ShouldDeferMaintenance(TerrainChunk chunk, double now, out double due)
    {
        due = now;
        if (!GeometryUploaded ||
            _activeMesh != null &&
            WorkerMeshState == TerrainMeshState.Building &&
            (CompletedMeshSlices > 0 || ActiveSlice != null) &&
            Matches(chunk, _activeMesh))
        {
            _maintenanceWait = null;
            return false;
        }

        var wait = _maintenanceWait;
        if (wait == null || !Matches(chunk, wait.Inputs))
        {
            wait = new MaintenanceWait(CaptureStamp(chunk), wait?.FirstSeen ?? now, now);
            _maintenanceWait = wait;
        }

        due = Math.Min(wait.ChangedAt + .05, wait.FirstSeen + .2);
        if (now < due) { return true; }

        _maintenanceWait = null;
        return false;
    }

    private MeshBuildStamp CaptureStamp(TerrainChunk chunk, bool allowPendingLighting = false)
    {
        return new MeshBuildStamp(
            chunk.AllocationGeneration,
            chunk.NetworkContentVersion,
            Volatile.Read(ref _requestedMeshRevision),
            CaptureNeighbors(chunk),
            chunk.Terrain.SeasonTemperature,
            chunk.Terrain.SeasonHumidity,
            allowPendingLighting
        );
    }

    private static TerrainLightSnapshot?[] CaptureLights(TerrainChunk chunk)
    {
        var snapshots = new TerrainLightSnapshot?[9];
        var index = 0;
        for (var x = -1; x <= 1; x++)
        {
            for (var z = -1; z <= 1; z++)
            {
                var neighbor = chunk.Terrain.GetChunkAtCoords(chunk.Coords.X + x, chunk.Coords.Y + z);
                if (neighbor != null)
                {
                    chunk.Terrain.Lighting.TryGetPublishedSnapshot(neighbor, out snapshots[index]);
                }

                index++;
            }
        }

        return snapshots;
    }

    /// <summary>调用时须持有所属地形的访问锁。</summary>
    public TerrainMeshState PrepareMeshUpdate(TerrainChunk chunk, bool allowPendingLighting = false)
    {
        if (_activeMesh is null || !Matches(chunk, _activeMesh))
        {
            _activeMesh = CaptureStamp(chunk, allowPendingLighting);
            _buildLights = CaptureLights(chunk);
            _reuseCompletedGeometry = false;
            if (_completedMesh is { SampledLightFingerprint: not null } previous &&
                previous.Generation == _activeMesh.Generation &&
                previous.Revision == _activeMesh.Revision &&
                SameNeighborGeometry(previous.Neighbors, _activeMesh.Neighbors))
            {
                _reuseCompletedGeometry = WorkerMeshState == TerrainMeshState.Ready &&
                                          previous.SeasonTemperature == _activeMesh.SeasonTemperature &&
                                          previous.SeasonHumidity == _activeMesh.SeasonHumidity &&
                                          SameNeighborGeometry(
                                              previous.Neighbors,
                                              _activeMesh.Neighbors,
                                              useHalo: false
                                          );
                _activeMesh = _activeMesh with
                {
                    LightReads = previous.LightReads,
                    SampledLightFingerprint = previous.LightReads.Fingerprint(_buildLights)
                };
            }

            WorkerMeshState = TerrainMeshState.Building;
            CompletedMeshSlices = 0;
            ActiveSlice = null;
            _reusedInteractiveSlices = false;
        }
        else if (allowPendingLighting && !_activeMesh.AllowPendingLighting && CanUsePublishedLighting(chunk))
        {
            // A background build may have started before a new propagation request. The foreground
            // interaction can finish those same inputs using their published light without restarting.
            var previous = _activeMesh;
            _activeMesh = previous with { AllowPendingLighting = true };
            if (ReferenceEquals(Volatile.Read(ref _completedMesh), previous))
            {
                Volatile.Write(ref _completedMesh, _activeMesh);
            }
        }

        return WorkerMeshState;
    }

    private static bool SameNeighborGeometry(NeighborInput[] first, NeighborInput[] second, bool useHalo = true)
    {
        for (var i = 0; i < first.Length; i++)
        {
            if (!MatchesGeometry(first[i].Geometry, second[i].Geometry, i, useHalo)) { return false; }
        }

        return true;
    }

    private static bool MatchesGeometry(NeighborGeometry previous, NeighborGeometry current, int index, bool useHalo)
    {
        return previous == current ||
               useHalo &&
               index != 4 &&
               previous.Generation == current.Generation &&
               previous.ReplicaHaloRevision.HasValue &&
               previous.ReplicaHaloRevision == current.ReplicaHaloRevision;
    }

    public void CompleteMeshSlice()
    {
        if (WorkerMeshState != TerrainMeshState.Building ||
            ActiveSlice != null ||
            CompletedMeshSlices >= TerrainChunk.SlicesCount)
        {
            throw new InvalidOperationException("No terrain mesh slice is awaiting completion.");
        }

        CompletedMeshSlices++;
    }

    /// <summary>仅发布仍与当前分配、内容和重建请求相符的构建结果。</summary>
    public bool CompleteMeshBuild(TerrainChunk chunk)
    {
        if (_activeMesh is null ||
            (!LightingInputsReadable(chunk) &&
             !(_activeMesh.AllowPendingLighting && CanUsePublishedLighting(chunk))) ||
            WorkerMeshState != TerrainMeshState.Building ||
            CompletedMeshSlices != TerrainChunk.SlicesCount ||
            !Matches(chunk, _activeMesh))
        {
            return false;
        }

        var lightReads = new TerrainMeshLightReads();
        foreach (ref readonly var reads in _sliceLightReads.AsSpan()) { lightReads.Include(in reads); }

        var recordedAllSlices = _recordedLightReads == (1 << TerrainChunk.SlicesCount) - 1;
        ulong? fingerprint = recordedAllSlices &&
                             Array.TrueForAll(_sliceTopologyValid, valid => valid) &&
                             !lightReads.HasOutsideNeighbors &&
                             _buildLights != null
            ? lightReads.Fingerprint(_buildLights)
            : null;
        if (!_activeMesh.AllowPendingLighting &&
            _activeMesh.SampledLightFingerprint.HasValue &&
            fingerprint.HasValue &&
            fingerprint != lightReads.FingerprintCurrent(chunk))
        {
            _activeMesh = null;
            _buildLights = null;
            return false;
        }

        // Completed/GPU stamps keep the light fingerprint and release the immutable cell storage.
        _buildLights = null;
        WorkerMeshState = TerrainMeshState.Ready;
        _activeMesh = _activeMesh with { LightReads = lightReads, SampledLightFingerprint = fingerprint };
        Volatile.Write(ref _completedMesh, _activeMesh);
        NewGeometryData = true;
        return true;
    }


    public volatile bool NewGeometryData;

    public void Dispose()
    {
        foreach (var buffer in _buffers ?? [])
        {
            buffer.Dispose();
        }

        _buffers?.Clear();
        var drawArray = _draws?.ToArray() ?? [];
        foreach (var draw in drawArray)
        {
            var drawValues = draw.Value.ToArray();
            foreach (var value in drawValues)
            {
                value.Dispose();
            }
        }

        _draws?.Clear();
        InvalidateSliceContentsHashes();
        GeometryUploaded = false;
        NewGeometryData = false;
        RequestMeshRebuild();
        Volatile.Write(ref _completedMesh, null);
        Volatile.Write(ref _uploadedMesh, null);
        _activeMesh = null;
        _buildLights = null;
        _maintenanceWait = null;
        _interactiveSlices = 0;
        _reusedInteractiveSlices = false;
        Array.Clear(_uploadedSliceInputs);
        WorkerMeshState = TerrainMeshState.Building;
        CompletedMeshSlices = 0;
        ActiveSlice = null;
    }

    public void InvalidateSliceContentsHashes()
    {
        Array.Clear(_sliceGeometryValid);
        Array.Clear(_sliceTopologyValid);
        Array.Clear(_sliceLightReads);
        _recordedLightReads = 0;
    }

    /// <summary>仅在完整网格的几何输入仍一致时，直接用新光照更新分片指纹。</summary>
    internal bool TryReuseSliceGeometry(int slice, out TerrainSliceInputs inputs)
    {
        if (_reuseCompletedGeometry &&
            _sliceGeometryValid[slice] &&
            _buildLights != null)
        {
            inputs = generatedSliceInputs[slice] with
            {
                Lighting = LightReadsForSlice(slice).Fingerprint(_buildLights)
            };
            return true;
        }

        inputs = default;
        return false;
    }

    internal bool CanRefreshSliceLighting(int slice, TerrainSliceInputs inputs)
    {
        return _sliceTopologyValid[slice] && generatedSliceInputs[slice].Geometry == inputs.Geometry;
    }

    internal bool MatchesSliceInputs(int slice, TerrainSliceInputs inputs)
    {
        return _sliceGeometryValid[slice] && generatedSliceInputs[slice] == inputs;
    }

    internal bool BeginSliceGeometryUpdate(int slice, TerrainSliceInputs inputs, bool geometryChanges = true)
    {
        if (MatchesSliceInputs(slice, inputs)) { return false; }

        // Invalidate before Draws can be cleared or partially generated, including throwing builds.
        _sliceGeometryValid[slice] = false;
        if (geometryChanges)
        {
            _sliceTopologyValid[slice] = false;
            _topologyRevision++;
        }

        return true;
    }

    internal void CompleteSliceGeometryUpdate(int slice, TerrainSliceInputs inputs, bool canReplayLighting = true)
    {
        generatedSliceInputs[slice] = inputs;
        _sliceGeometryValid[slice] = true;
        _sliceTopologyValid[slice] = canReplayLighting;
    }

    internal TerrainMeshLightReads LightReadsForSlice(int slice)
    {
        return (_recordedLightReads & (1 << slice)) != 0 && !_sliceLightReads[slice].HasOutsideNeighbors
            ? _sliceLightReads[slice]
            : TerrainMeshLightReads.AroundSlice(slice);
    }

    internal void MarkSliceLightReads(int slice, in TerrainMeshLightReads reads)
    {
        _sliceLightReads[slice] = reads;
        _recordedLightReads |= 1 << slice;
    }
}
