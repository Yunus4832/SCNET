using Engine.Graphics;

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

    public readonly Dictionary<int, float> HazeEnds = new();

    public bool GeometryUploaded;

    public long ClientGeometryContentVersion => Volatile.Read(ref _completedMesh)?.ContentVersion ?? 0;

    private long _requestedMeshRevision;

    private MeshBuildStamp? _activeMesh;

    private MeshBuildStamp? _completedMesh;

    private MeshBuildStamp? _uploadedMesh;

    public long UploadedContentVersion => Volatile.Read(ref _uploadedMesh)?.ContentVersion ?? 0;

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
        GeometryUploaded = true;
    }

    public TerrainMeshState WorkerMeshState { get; private set; }


    private readonly record struct NeighborInput(
        ulong Generation,
        long ContentVersion,
        long Heights,
        long Lighting,
        TerrainDerivedRevisions.Request Request
    );

    private sealed record MaintenanceWait(MeshBuildStamp Inputs, double FirstSeen, double ChangedAt);

    private MaintenanceWait? _maintenanceWait;

    private sealed record MeshBuildStamp(
        ulong Generation,
        long ContentVersion,
        long Revision,
        NeighborInput[] Neighbors
    );

    public void RequestMeshRebuild()
    {
        Interlocked.Increment(ref _requestedMeshRevision);
    }

    public bool HasCurrentGeometry(TerrainChunk chunk)
    {
        var completed = Volatile.Read(ref _completedMesh);
        return completed is not null && Matches(chunk, completed);
    }

    private bool Matches(TerrainChunk chunk, MeshBuildStamp stamp)
    {
        return stamp.Generation == chunk.AllocationGeneration &&
               stamp.ContentVersion == chunk.NetworkContentVersion &&
               stamp.Revision == Volatile.Read(ref _requestedMeshRevision) &&
               MatchesNeighbors(chunk, stamp.Neighbors);
    }

    private static NeighborInput ReadNeighbor(TerrainChunk chunk, int x, int z)
    {
        var neighbor = chunk.Terrain.GetChunkAtCoords(chunk.Coords.X + x, chunk.Coords.Y + z);
        return neighbor is not { HasContents: true }
            ? default
            : new NeighborInput(
                neighbor.AllocationGeneration,
                neighbor.NetworkContentVersion,
                neighbor.DerivedRevisions.HeightRevision,
                neighbor.DerivedRevisions.LightRevision,
                neighbor.DerivedRevisions.Capture()
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

    private static bool MatchesNeighbors(TerrainChunk chunk, NeighborInput[] neighbors)
    {
        var index = 0;
        for (var x = -1; x <= 1; x++)
        {
            for (var z = -1; z <= 1; z++)
            {
                if (neighbors[index++] != ReadNeighbor(chunk, x, z)) { return false; }
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

    /// <summary>Coalesces already-visible maintenance without delaying initial geometry or a valid second pass.</summary>
    internal bool ShouldDeferMaintenance(TerrainChunk chunk, double now, out double due)
    {
        due = now;
        if (!GeometryUploaded ||
            _activeMesh != null &&
            WorkerMeshState == TerrainMeshState.NeedsSecondPass &&
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

    private MeshBuildStamp CaptureStamp(TerrainChunk chunk)
    {
        return new MeshBuildStamp(
            chunk.AllocationGeneration,
            chunk.NetworkContentVersion,
            Volatile.Read(ref _requestedMeshRevision),
            CaptureNeighbors(chunk)
        );
    }

    /// <summary>Called only by the synchronized terrain worker.</summary>
    public TerrainMeshState PrepareMeshUpdate(TerrainChunk chunk)
    {
        if (_activeMesh is null || !Matches(chunk, _activeMesh))
        {
            _activeMesh = CaptureStamp(chunk);
            WorkerMeshState = TerrainMeshState.NeedsFirstPass;
        }

        return WorkerMeshState;
    }

    public void CompleteFirstPass() => WorkerMeshState = TerrainMeshState.NeedsSecondPass;

    /// <summary>Publishes only a build for the same allocation, content and rebuild request.</summary>
    public bool CompleteMeshBuild(TerrainChunk chunk)
    {
        if (!LightingInputsReadable(chunk) ||
            _activeMesh is null ||
            WorkerMeshState != TerrainMeshState.NeedsSecondPass ||
            !Matches(chunk, _activeMesh))
        {
            return false;
        }

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
        _maintenanceWait = null;
        WorkerMeshState = TerrainMeshState.NeedsFirstPass;
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
