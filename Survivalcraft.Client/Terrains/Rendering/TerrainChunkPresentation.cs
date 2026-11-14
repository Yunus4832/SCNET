using Engine.Graphics;

using Game.Terrains.Lighting;

namespace Game.Terrains.Rendering;

/// <summary>Client-owned meshes and GPU resources for one chunk allocation.</summary>
public sealed class TerrainChunkPresentation : IDisposable
{
    private DynamicArray<TerrainChunkGeometry.Buffer>? _buffers;

    public DynamicArray<TerrainChunkGeometry.Buffer> Buffers => _buffers ??= [];

    public TerrainGeometry[] ChunkSliceGeometries => field ??= new TerrainGeometry[TerrainChunk.SlicesCount];

    public float DrawDistanceSquared;

    private Dictionary<Texture2D, TerrainGeometry[]>? _draws;

    public Dictionary<Texture2D, TerrainGeometry[]> Draws => _draws ??= new();

    // Hashes describe the actual CPU Draws, independently of uploaded GPU buffers.
    public readonly int[] GeneratedSliceContentsHashes = new int[TerrainChunk.SlicesCount];

    private readonly bool[] _sliceGeometryValid = new bool[TerrainChunk.SlicesCount];

    public bool HasGeometry => _geometry is not null;

    private TerrainChunkGeometry? _geometry;

    public TerrainChunkGeometry Geometry => _geometry ??= new TerrainChunkGeometry();

    public bool GeometryUploaded;

    public long ClientGeometryContentVersion => Volatile.Read(ref _completedMesh)?.ContentVersion ?? 0;

    private long _requestedMeshRevision;

    private MeshBuildStamp? _activeMesh;

    private MeshBuildStamp? _completedMesh;

    private MeshBuildStamp? _uploadedMesh;

    private TerrainLightSnapshot?[]? _buildLights;

    private readonly int[] _uploadedSliceHashes = new int[TerrainChunk.SlicesCount];

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
            GeneratedSliceContentsHashes[slice] != _uploadedSliceHashes[slice])
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

    /// <summary>Opaque identity of the completed CPU mesh being staged for upload.</summary>
    internal object? GeometryUploadToken => Volatile.Read(ref _completedMesh);

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

    /// <summary>Called under the geometry lock only after the actual GPU upload succeeded.</summary>
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
        Array.Copy(GeneratedSliceContentsHashes, _uploadedSliceHashes, TerrainChunk.SlicesCount);
        GeometryUploaded = true;
    }

    public TerrainMeshState WorkerMeshState { get; private set; }

    public int CompletedMeshSlices { get; private set; }

    internal SliceWork? ActiveSlice { get; set; }

    internal sealed class SliceWork(int slice, int contentsHash)
    {
        public int Slice { get; } = slice;

        public int ContentsHash { get; } = contentsHash;

        public int Column;

        public int CompactionBuffer;
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


    private readonly record struct NeighborInput(
        ulong Generation,
        long ContentVersion,
        long ContentRevision,
        long Heights,
        long Lighting
    );

    private sealed record MaintenanceWait(MeshBuildStamp Inputs, double FirstSeen, double ChangedAt);

    private MaintenanceWait? _maintenanceWait;

    private sealed record MeshBuildStamp(
        ulong Generation,
        long ContentVersion,
        long Revision,
        NeighborInput[] Neighbors,
        bool AllowPendingLighting
    );

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
        if (!GeometryUploaded || !chunk.HasContents) { return false; }

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
        return stamp.Generation == chunk.AllocationGeneration &&
               stamp.ContentVersion == chunk.NetworkContentVersion &&
               stamp.Revision == Volatile.Read(ref _requestedMeshRevision) &&
               MatchesNeighbors(chunk, stamp.Neighbors, stamp.AllowPendingLighting);
    }

    private static NeighborInput ReadNeighbor(TerrainChunk chunk, int x, int z)
    {
        var neighbor = chunk.Terrain.GetChunkAtCoords(chunk.Coords.X + x, chunk.Coords.Y + z);
        return neighbor is not { HasContents: true }
            ? default
            : new NeighborInput(
                neighbor.AllocationGeneration,
                neighbor.NetworkContentVersion,
                neighbor.NetworkContentRevision,
                neighbor.DerivedRevisions.HeightRevision,
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

    private static bool MatchesNeighbors(TerrainChunk chunk, NeighborInput[] neighbors, bool allowPendingLighting)
    {
        var index = 0;
        for (var x = -1; x <= 1; x++)
        {
            for (var z = -1; z <= 1; z++)
            {
                var expected = neighbors[index++];
                var current = ReadNeighbor(chunk, x, z);
                if (allowPendingLighting) { current = current with { Lighting = expected.Lighting }; }

                if (expected != current) { return false; }
            }
        }

        return true;
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

    /// <summary>Coalesces already-visible maintenance without delaying initial geometry or valid in-progress slices.</summary>
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

    /// <summary>Called while holding the owning terrain's access gate.</summary>
    public TerrainMeshState PrepareMeshUpdate(TerrainChunk chunk, bool allowPendingLighting = false)
    {
        if (_activeMesh is null || !Matches(chunk, _activeMesh))
        {
            _activeMesh = CaptureStamp(chunk, allowPendingLighting);
            _buildLights = CaptureLights(chunk);
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

    /// <summary>Publishes only a build for the same allocation, content and rebuild request.</summary>
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

        // Snapshot references are needed only while generating; completed/GPU stamps must not
        // retain superseded light volumes for every visible chunk.
        _buildLights = null;
        WorkerMeshState = TerrainMeshState.Ready;
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
        Array.Clear(_uploadedSliceHashes);
        WorkerMeshState = TerrainMeshState.Building;
        CompletedMeshSlices = 0;
        ActiveSlice = null;
    }

    public void InvalidateSliceContentsHashes()
    {
        Array.Clear(_sliceGeometryValid);
    }

    internal bool BeginSliceGeometryUpdate(int slice, int contentsHash)
    {
        if (_sliceGeometryValid[slice] && GeneratedSliceContentsHashes[slice] == contentsHash)
        {
            return false;
        }

        // Invalidate before Draws can be cleared or partially generated, including throwing builds.
        _sliceGeometryValid[slice] = false;
        return true;
    }

    internal void CompleteSliceGeometryUpdate(int slice, int contentsHash)
    {
        GeneratedSliceContentsHashes[slice] = contentsHash;
        _sliceGeometryValid[slice] = true;
    }
}
